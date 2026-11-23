using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Input;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// The health window (H): your own body, or that of whoever is within reach, with the treatments you can give - see Rendering/HealthWindow.cs for
// what it shows and World.Treatment.cs for what the server does with the requests.
public partial class Game1
{
    private const float HealthReach = 2.4f;

    private HealthWindow _healthWindow = null!;
    private bool _healthWindowOpen;
    private int _healthTargetId;
    private BodyPart? _healthSelectedPart;
    private bool _healthCprHeld;
    private bool _prevOpenHealthKeyDown;
    private TreatRequest? _pendingTreat;
    private SurgeryRequest? _pendingSurgery;
    private bool _pendingCancelTreatment;
    private bool _pendingGiveUp;

    // You and everyone within reach (same structure, close enough to lay hands on).
    private List<CharacterState> HealthTargets(WorldSnapshot snapshot, CharacterState me)
    {
        var list = new List<CharacterState> { me };
        foreach (var other in snapshot.Characters)
        {
            if (other.PlayerId == me.PlayerId || other.Health <= 0f || other.Body is null)
                continue;
            if (other.IsOutside != me.IsOutside || other.OnStation != me.OnStation || other.OnEnemyShip != me.OnEnemyShip)
                continue;
            if (Math.Sqrt((other.X - me.X) * (other.X - me.X) + (other.Y - me.Y) * (other.Y - me.Y)) <= HealthReach)
                list.Add(other);
        }
        return list;
    }

    private static BodyPart WorstPart(BodyState body) =>
        body.Parts.OrderByDescending(p => p.Trauma + p.Burn + p.Bleeding * 2f + (p.Fractured ? 30f : 0f)).First().Part;

    // Called once a frame from Update: the H key opens/closes the window, and holding the mouse on "chest compressions" keeps pressing.
    private void UpdateHealthInput(KeyboardState keyboard, MouseState mouse)
    {
        var snapshot = _client.LatestSnapshot;
        var me = snapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        var openKeyDown = !_chatFocused && !_shipEditorOpen && keyboard.IsKeyDown(_keyBindings.Get(PlayerAction.OpenHealth));
        if (openKeyDown && !_prevOpenHealthKeyDown && snapshot is not null && me?.Body is { } myBody && me.Health > 0f)
        {
            _healthWindowOpen = !_healthWindowOpen;
            if (_healthWindowOpen)
            {
                _healthTargetId = me.PlayerId;
                _healthSelectedPart = myBody.Parts.Any(p => p.Trauma + p.Burn + p.Bleeding > 0.5f || p.Fractured) ? WorstPart(myBody) : null;
            }
        }
        _prevOpenHealthKeyDown = openKeyDown;

        if (!_healthWindowOpen)
        {
            _healthCprHeld = false;
            return;
        }
        if (snapshot is null || me?.Body is null || me.Health <= 0f)
        {
            _healthWindowOpen = false;
            _healthCprHeld = false;
            return;
        }

        // The target has to stay within reach; otherwise back to yourself.
        var targets = HealthTargets(snapshot, me);
        if (targets.All(t => t.PlayerId != _healthTargetId))
        {
            _healthTargetId = me.PlayerId;
            _healthSelectedPart = null;
        }
        var target = targets.First(t => t.PlayerId == _healthTargetId);
        _healthCprHeld = mouse.LeftButton == ButtonState.Pressed && HealthWindow.CprRect.Contains(_designMouse)
            && target.PlayerId != me.PlayerId && target.Body is { ArrestSecondsLeft: > 0f };
    }

    // A click inside the window. True when it was the window's (always, inside its rectangle).
    private bool HandleHealthWindowClick(WorldSnapshot snapshot)
    {
        if (!_healthWindowOpen || !HealthWindow.Contains(_designMouse))
            return false;
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me is null)
            return true;
        var targets = HealthTargets(snapshot, me);
        var target = targets.FirstOrDefault(t => t.PlayerId == _healthTargetId) ?? me;
        var hit = _healthWindow.HitTest(_designMouse, me, targets);
        switch (hit.Kind)
        {
            case HealthHitKind.Close:
                _healthWindowOpen = false;
                break;
            case HealthHitKind.Target:
                _healthTargetId = targets[hit.Index].PlayerId;
                _healthSelectedPart = targets[hit.Index].Body is { } body && body.Parts.Any(p => p.Trauma + p.Burn + p.Bleeding > 0.5f || p.Fractured) ? WorstPart(body) : null;
                break;
            case HealthHitKind.Part:
                _healthSelectedPart = hit.Part;
                break;
            case HealthHitKind.WholeBody:
                _healthSelectedPart = null;
                break;
            case HealthHitKind.Cancel:
                _pendingCancelTreatment = true;
                break;
            case HealthHitKind.Item when hit.Item is { } item && target.Body is { } targetBody:
                _pendingTreat = new TreatRequest(target.PlayerId, ChoosePartFor(item, targetBody), item);
                break;
            case HealthHitKind.Surgery when hit.Organ is { } organ && target.PlayerId != me.PlayerId:
                _pendingSurgery = new SurgeryRequest(target.PlayerId, organ);
                break;
        }
        return true;
    }

    // Which part an item is used on: the selected one; for something that works on the whole body, any; with nothing selected, the part it would help most.
    private BodyPart ChoosePartFor(ItemType item, BodyState bodyState)
    {
        if (MedicalTreatment.IsWholeBody(item))
            return _healthSelectedPart ?? BodyPart.Torso;
        if (_healthSelectedPart is { } selected)
            return selected;
        var body = new BodyHealth();
        body.LoadState(bodyState);
        var ordered = bodyState.Parts.OrderByDescending(p => p.Trauma + p.Burn + p.Bleeding * 2f + (p.Fractured ? 30f : 0f)).Select(p => p.Part);
        foreach (var part in ordered)
            if (MedicalTreatment.Recommended(body, part).Contains(item))
                return part;
        return BodyPart.Torso;
    }

    private void DrawHealthWindow(WorldSnapshot snapshot, float totalSeconds)
    {
        if (!_healthWindowOpen)
            return;
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me?.Body is null)
            return;
        var targets = HealthTargets(snapshot, me);
        var target = targets.FirstOrDefault(t => t.PlayerId == _healthTargetId) ?? me;
        _healthWindow.Draw(_spriteBatch, me, target, targets, _healthSelectedPart, _designMouse, totalSeconds);
    }
}
