using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// First aid as a standing job rather than an item: what a hired scientist does for the crew and a hostile scientist does for his own.
// No consumables - a bot has nothing to run out of - but the same body rules as everybody else: compressions when the heart has stopped,
// then a dressing / splint / antiseptic every few seconds, then plain recovery.
public sealed partial class World
{
    private const float FirstAidActionSeconds = 3f;

    private static BodyPart? FirstPartWhere(BodyHealth body, Func<BodyPart, bool> filter, Func<BodyPart, float> rank) =>
        body.AllParts.Where(filter).OrderByDescending(rank).Select(p => (BodyPart?)p).FirstOrDefault();

    private static void AdministerFirstAid(BodyHealth body, float dt, float healPerSecond, ref float actionTimer)
    {
        if (body.Dead)
            return;
        if (body.InArrest)
        {
            body.Compress();
            return;
        }

        actionTimer -= dt;
        if (actionTimer <= 0f)
        {
            actionTimer = FirstAidActionSeconds;
            if (FirstPartWhere(body, p => body[p].Bleeding > 0.5f && !body[p].Bandaged && !body[p].Tourniquet, p => body[p].Bleeding) is { } bleeding)
                body.ApplyBandage(bleeding);
            else if (FirstPartWhere(body, p => body[p].Fractured && !body[p].Splinted && !body[p].Cast, _ => 0f) is { } broken)
                body.ApplySplint(broken);
            else if (FirstPartWhere(body, p => body[p].Infection > 5f && body[p].DisinfectedSeconds <= 0f, p => body[p].Infection) is { } infected)
                body.ApplyAntiseptic(infected);
        }
        body.Heal(healPerSecond * dt);
        body.RelieveConcussion(healPerSecond * dt);
    }
}
