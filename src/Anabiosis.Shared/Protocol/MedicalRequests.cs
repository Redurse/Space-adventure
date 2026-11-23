using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// Use a medical item from your own inventory on one body part of a person (yourself included) - the health window's click.
public sealed record TreatRequest(int TargetId, BodyPart Part, ItemType Item);

// Operate on one organ of a person who is out cold (needs a surgical kit).
public sealed record SurgeryRequest(int TargetId, Organ Organ);

// What a character is busy doing right now, for the progress bar: a treatment with an item, an operation, or chest compressions.
public sealed record TreatmentState(int TargetId, BodyPart? Part, ItemType? Item, Organ? Organ, float Remaining, float Total, bool IsCpr);
