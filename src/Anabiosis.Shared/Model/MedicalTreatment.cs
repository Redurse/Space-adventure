namespace Anabiosis.Shared.Model;

// What each medical item does when used on a body part (BodyHealth's treatment methods), how long it takes, and which items the health window
// recommends for what is wrong - the "recommended treatments" row of Barotrauma's health interface. Pure rules, shared by the server (which
// applies them) and the client (which shows them).
public static class MedicalTreatment
{
    public static readonly ItemType[] Items =
    {
        ItemType.MedKit, ItemType.Bandage, ItemType.Tourniquet, ItemType.Antiseptic, ItemType.Splint, ItemType.PlasterCast,
        ItemType.Painkiller, ItemType.Antibiotic, ItemType.BurnGel, ItemType.Adrenaline, ItemType.Defibrillator, ItemType.SurgeryKit,
    };

    public static bool IsMedical(ItemType item) => Array.IndexOf(Items, item) >= 0;

    // Whether the item is used up when it works (the defibrillator and the surgical kit are tools that last).
    public static bool IsConsumed(ItemType item) => item is not (ItemType.Defibrillator or ItemType.SurgeryKit);

    // Items that act on the whole body or a stopped heart rather than on one part.
    public static bool IsWholeBody(ItemType item) =>
        item is ItemType.MedKit or ItemType.Painkiller or ItemType.Antibiotic or ItemType.Adrenaline or ItemType.Defibrillator or ItemType.SurgeryKit;

    public static float Seconds(ItemType item) => item switch
    {
        ItemType.Bandage => 2f,
        ItemType.Tourniquet => 2f,
        ItemType.Antiseptic => 2.5f,
        ItemType.Splint => 4f,
        ItemType.PlasterCast => 7f,
        ItemType.Painkiller => 1.5f,
        ItemType.Antibiotic => 2f,
        ItemType.BurnGel => 3f,
        ItemType.Adrenaline => 1.5f,
        ItemType.Defibrillator => 3f,
        ItemType.MedKit => 4f,
        _ => 2f,
    };

    // The surgeon needs this long to work on an organ.
    public const float SurgerySeconds = 14f;
    public const float SurgeryHealsPerOperation = 45f;
    public const float DefibrillatorCooldownSeconds = 8f;
    // Treating yourself is clumsier than being treated by someone else.
    public const float SelfTreatmentSlowdown = 1.6f;

    // Uses the item on the part; returns whether it did any good (what is used up is never wasted on someone who did not need it).
    public static bool Apply(BodyHealth body, ItemType item, BodyPart part, float roll)
    {
        switch (item)
        {
            case ItemType.MedKit:
                if (!body.NeedsTreatment)
                    return false;
                body.StopAllBleeding();
                body.RecoverBlood(40f);
                body.Heal(50f);
                return true;
            case ItemType.Bandage: return body.ApplyBandage(part);
            case ItemType.Tourniquet: return body.ApplyTourniquet(part);
            case ItemType.Antiseptic: return body.ApplyAntiseptic(part);
            case ItemType.Splint: return body.ApplySplint(part);
            case ItemType.PlasterCast: return body.ApplyCast(part);
            case ItemType.Painkiller: return body.GivePainkiller(120f);
            case ItemType.Antibiotic: return body.GiveAntibiotic();
            case ItemType.BurnGel: return body.ApplyBurnGel(part);
            case ItemType.Adrenaline: return body.GiveAdrenaline();
            case ItemType.Defibrillator: return body.Defibrillate(roll);
            default: return false;
        }
    }

    // Which items would help with what is wrong with this part (and the body as a whole), most urgent first.
    public static IReadOnlyList<ItemType> Recommended(BodyHealth body, BodyPart? selected)
    {
        var items = new List<ItemType>();
        if (body.InArrest)
        {
            items.Add(ItemType.Defibrillator);
            items.Add(ItemType.Adrenaline);
        }
        var parts = selected is { } one ? new[] { one } : body.AllParts.ToArray();
        foreach (var part in parts)
        {
            var injury = body[part];
            if (injury.Bleeding > 0.5f && !injury.Bandaged && !injury.Tourniquet)
            {
                items.Add(ItemType.Bandage);
                if (part is not (BodyPart.Head or BodyPart.Torso))
                    items.Add(ItemType.Tourniquet);
            }
            if (injury.Tourniquet)
                items.Add(ItemType.Tourniquet); // to take it off again
            if (injury.Fractured && !injury.Cast)
            {
                if (!injury.Splinted)
                    items.Add(ItemType.Splint);
                items.Add(ItemType.PlasterCast);
            }
            if (injury.Burn > 1f)
                items.Add(ItemType.BurnGel);
            if ((injury.Bleeding > 0.5f || injury.Burn > 15f || injury.Infection > 1f) && injury.DisinfectedSeconds <= 0f)
                items.Add(ItemType.Antiseptic);
        }
        if (body.Sepsis > 1f || body.AllParts.Any(p => body[p].Infection > 20f))
            items.Add(ItemType.Antibiotic);
        if (body.Pain > 30f && body.AnalgesiaSeconds <= 0f && !body.Overdosing)
            items.Add(ItemType.Painkiller);
        if (body.Unconscious && !body.InArrest && body.Concussion > 20f)
            items.Add(ItemType.Adrenaline);
        if (body.HeartDamage > 1f || body.LungDamage > 1f || body.BrainDamage > 1f)
            items.Add(ItemType.SurgeryKit);
        if (body.NeedsTreatment)
            items.Add(ItemType.MedKit);
        return items.Distinct().ToList();
    }

    public static string PartName(BodyPart part) => part switch
    {
        BodyPart.Head => "Голова",
        BodyPart.Torso => "Торс",
        BodyPart.LeftArm => "Левая рука",
        BodyPart.RightArm => "Правая рука",
        BodyPart.LeftLeg => "Левая нога",
        _ => "Правая нога",
    };

    public static string OrganName(Organ organ) => organ switch
    {
        Organ.Heart => "Сердце",
        Organ.Lungs => "Лёгкие",
        _ => "Мозг",
    };
}
