namespace Anabiosis.Shared.Model;

// Which game the ship's one CardTable is currently offering/running (World.CardTable.cs) - picked
// by whichever of the 2 seated crew presses a choice button first, once the table is free (neither
// game already active). Durak is the original "Дурак переводной" (World.CardGame.cs); Fronts is
// "Фронты" (World.FrontsGame.cs), a simplified Hearts of Iron IV-style 2-player wargame - both
// direct user requests.
public enum CardTableGameKind
{
    Durak,
    Fronts,
}
