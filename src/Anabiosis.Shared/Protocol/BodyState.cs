using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

public sealed record BodyPartState(BodyPart Part, float Trauma, float Burn, float Bleeding, bool Fractured,
    float Infection = 0f, bool Bandaged = false, bool Tourniquet = false, bool Splinted = false, bool Cast = false, float CastSeconds = 0f,
    float DisinfectedSeconds = 0f);

// A character's medical state as the client sees it (the health window, the body diagram, the unconscious screen) - everything BodyHealth
// holds, so the client can rebuild the same BodyHealth (LoadState) and ask it what is wrong and which treatments help.
public sealed record BodyState(IReadOnlyList<BodyPartState> Parts, float BloodLoss, float Hypoxia, float Pain, float Concussion,
    bool Unconscious, float ArrestSecondsLeft, float AnalgesiaSeconds, bool IsDowned,
    float Sepsis = 0f, float HeartDamage = 0f, float LungDamage = 0f, float BrainDamage = 0f, float Dose = 0f,
    float AdrenalineSeconds = 0f, float AntibioticSeconds = 0f, float OtherLoss = 0f, bool Dead = false);
