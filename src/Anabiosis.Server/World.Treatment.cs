using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Treating people (the health window, H): use a medical item on a body part of yourself or someone next to you, operate on an organ of
// someone who is out cold, or give chest compressions to a stopped heart. Treatments take time (a splint longer than a bandage; being treated
// by someone else is quicker than treating yourself), are interrupted if the doctor moves off, is hurt badly or the patient is out of
// reach, and use the item up when they work. All of it goes through MedicalTreatment's rules, which the client reads to show what helps.
public sealed partial class World
{
    private const float TreatReach = 2.4f;
    private const float TreatMoveTolerance = 1.2f;

    private sealed class ActiveTreatment
    {
        public required int TargetId;
        public BodyPart Part;
        public ItemType? Item;
        public Organ? Organ;
        public float Remaining;
        public float Total;
        public Vec2 StartPosition;
    }

    private readonly Dictionary<int, ActiveTreatment> _treatments = new();
    private readonly Dictionary<int, int> _cprInput = new();
    private readonly Dictionary<int, float> _defibrillatorCooldown = new();

    // Can this doctor get at that person: yourself always; someone else only on the same structure within arm's reach.
    private static bool InTreatmentReach(Character doctor, Character patient)
    {
        if (ReferenceEquals(doctor, patient))
            return true;
        if (doctor.IsOutside != patient.IsOutside || doctor.OnStation != patient.OnStation || doctor.OnEnemyShip != patient.OnEnemyShip)
            return false;
        return (doctor.Position - patient.Position).Length() <= TreatReach;
    }

    // The medbay (a "Медотсек" from the shipwright catalog): treatment is faster in it and the patient slowly mends on their own.
    private const float MedbayTreatmentFactor = 0.65f;
    private const float MedbayRecoveryPerSecond = 1.2f;

    private bool IsInMedbay(Character character) =>
        !character.IsOutside && !character.OnStation && !character.OnEnemyShip
        && Ship.Rooms.FirstOrDefault(r => r.Id == character.RoomId) is { } room
        && CompartmentPricing.EntryForRoomName(room.Name)?.Type == CompartmentType.Medical;

    private static bool HasMedicalItem(Character character, ItemType item) =>
        character.Inventory.Has(item) || character.Inventory.BeltBagSlots.Any(s => s == item);

    private static void TakeMedicalItem(Character character, ItemType item)
    {
        if (character.Inventory.TryRemove(item))
            return;
        var slot = Array.IndexOf(character.Inventory.BeltBagSlots, (ItemType?)item);
        if (slot >= 0)
            character.Inventory.BeltBagSlots[slot] = null;
    }

    // Whether using this item on that part would do anything, so a treatment is never started for nothing.
    private static bool WouldHelp(BodyHealth body, ItemType item, BodyPart part) =>
        MedicalTreatment.Recommended(body, MedicalTreatment.IsWholeBody(item) ? null : part).Contains(item)
        || (item == ItemType.Tourniquet && body[part].Tourniquet);

    private void TryStartTreatment(Character doctor, TreatRequest request)
    {
        if (doctor.IsDead || doctor.IsDowned || !doctor.Body.HandsUsable)
            return;
        if (!_characters.TryGetValue(request.TargetId, out var patient) || patient.IsDead || !InTreatmentReach(doctor, patient))
            return;
        if (!MedicalTreatment.IsMedical(request.Item) || request.Item == ItemType.SurgeryKit || !HasMedicalItem(doctor, request.Item))
            return;
        if (request.Item == ItemType.Defibrillator && _defibrillatorCooldown.GetValueOrDefault(doctor.PlayerId) > 0f)
            return;
        if (!WouldHelp(patient.Body, request.Item, request.Part))
            return;

        var seconds = MedicalTreatment.Seconds(request.Item) * (ReferenceEquals(doctor, patient) ? MedicalTreatment.SelfTreatmentSlowdown : 1f) * (IsInMedbay(patient) ? MedbayTreatmentFactor : 1f);
        _treatments[doctor.PlayerId] = new ActiveTreatment
        {
            TargetId = request.TargetId, Part = request.Part, Item = request.Item, Remaining = seconds, Total = seconds, StartPosition = doctor.Position,
        };
    }

    private void TryStartSurgery(Character surgeon, SurgeryRequest request)
    {
        if (surgeon.IsDead || surgeon.IsDowned || !surgeon.Body.HandsUsable || !HasMedicalItem(surgeon, ItemType.SurgeryKit))
            return;
        if (!_characters.TryGetValue(request.TargetId, out var patient) || ReferenceEquals(surgeon, patient) || patient.IsDead
            || !InTreatmentReach(surgeon, patient))
            return;
        // Nobody can be cut open awake: they have to be out cold.
        if (!patient.Body.Unconscious || patient.Body.OrganDamage(request.Organ) <= 0.5f)
            return;

        _treatments[surgeon.PlayerId] = new ActiveTreatment
        {
            TargetId = request.TargetId, Organ = request.Organ, Item = ItemType.SurgeryKit,
            Remaining = MedicalTreatment.SurgerySeconds * (IsInMedbay(patient) ? MedbayTreatmentFactor : 1f),
            Total = MedicalTreatment.SurgerySeconds * (IsInMedbay(patient) ? MedbayTreatmentFactor : 1f), StartPosition = surgeon.Position,
        };
    }

    private void StepTreatments(double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        foreach (var key in _defibrillatorCooldown.Keys.ToList())
            _defibrillatorCooldown[key] = Math.Max(0f, _defibrillatorCooldown[key] - dt);

        foreach (var (doctorId, treatment) in _treatments.ToList())
        {
            if (!_characters.TryGetValue(doctorId, out var doctor) || !_characters.TryGetValue(treatment.TargetId, out var patient)
                || doctor.IsDead || doctor.IsDowned || patient.IsDead || !InTreatmentReach(doctor, patient)
                || (doctor.Position - treatment.StartPosition).Length() > TreatMoveTolerance
                || (treatment.Item is { } item && !HasMedicalItem(doctor, item)))
            {
                _treatments.Remove(doctorId);
                continue;
            }

            treatment.Remaining -= dt;
            if (treatment.Remaining > 0f)
                continue;

            _treatments.Remove(doctorId);
            FinishTreatment(doctor, patient, treatment);
        }

        foreach (var (doctorId, targetId) in _cprInput.ToList())
        {
            if (!_characters.TryGetValue(doctorId, out var doctor) || !_characters.TryGetValue(targetId, out var patient)
                || doctor.IsDead || doctor.IsDowned || ReferenceEquals(doctor, patient) || !InTreatmentReach(doctor, patient)
                || !patient.Body.InArrest || _treatments.ContainsKey(doctorId))
            {
                if (_characters.TryGetValue(doctorId, out var gone) && gone.IsDowned)
                    _cprInput.Remove(doctorId);
                continue;
            }
            patient.Body.Compress();
        }
    }

    private void FinishTreatment(Character doctor, Character patient, ActiveTreatment treatment)
    {
        if (treatment.Organ is { } organ)
        {
            if (patient.Body.Operate(organ, MedicalTreatment.SurgeryHealsPerOperation))
            {
                // A wound cut open with dirty hands festers.
                var cut = organ == Organ.Brain ? BodyPart.Head : BodyPart.Torso;
                if (patient.Body[cut].DisinfectedSeconds <= 0f)
                    patient.Body.AddInfection(cut, 12f);
            }
            return;
        }

        if (treatment.Item is not { } item)
            return;
        var worked = MedicalTreatment.Apply(patient.Body, item, treatment.Part, (float)_random.NextDouble());
        if (item == ItemType.Defibrillator)
            _defibrillatorCooldown[doctor.PlayerId] = MedicalTreatment.DefibrillatorCooldownSeconds;
        if (worked && MedicalTreatment.IsConsumed(item))
            TakeMedicalItem(doctor, item);
    }

    private TreatmentState? CreateTreatmentState(Character character)
    {
        if (_treatments.TryGetValue(character.PlayerId, out var treatment))
            return new TreatmentState(treatment.TargetId, treatment.Organ is null ? treatment.Part : null, treatment.Item, treatment.Organ,
                treatment.Remaining, treatment.Total, IsCpr: false);
        if (_cprInput.TryGetValue(character.PlayerId, out var targetId) && _characters.TryGetValue(targetId, out var patient)
            && patient.Body.InArrest && InTreatmentReach(character, patient))
            return new TreatmentState(targetId, null, null, null, 0f, 0f, IsCpr: true);
        return null;
    }

    // Test hook: whether a character is in the middle of a treatment.
    public bool DebugIsTreating(int playerId) => _treatments.ContainsKey(playerId);

    // The commands side of it, called from ApplyCommand.
    private void ApplyMedicalCommand(Character character, ClientCommand command)
    {
        if (command.CancelTreatment)
            _treatments.Remove(character.PlayerId);
        if (command.Treat is { } treat)
            TryStartTreatment(character, treat);
        if (command.Surgery is { } surgery)
            TryStartSurgery(character, surgery);
        if (command.CprTargetId is { } cprTarget)
            _cprInput[character.PlayerId] = cprTarget;
        else
            _cprInput.Remove(character.PlayerId);
    }
}
