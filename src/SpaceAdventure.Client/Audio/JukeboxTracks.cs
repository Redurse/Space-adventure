namespace SpaceAdventure.Client.Audio;

// The playlist for the in-world jukebox device, separate from GameMusic's shuffled ambience bag:
// a jukebox plays one track the player picked, by index, until they pick another or turn it off.
public static class JukeboxTracks
{
    public readonly record struct Track(string AssetName, string Title);

    // Asset names are paths under Content/Music/Jukebox, without extension.
    public static readonly Track[] All =
    {
        new("Music/Jukebox/jukebox_01_imperium_of_man", "Imperium of Man"),
        new("Music/Jukebox/jukebox_02_true_hymn_of_the_imperium", "Истинный гимн Империума"),
        new("Music/Jukebox/jukebox_03_voices_of_the_void_main_menu", "Voices of the Void — Главное меню"),
        new("Music/Jukebox/jukebox_04_sweden", "Sweden"),
        new("Music/Jukebox/jukebox_05_accordion_irish_remix", "Accordion (Some Kind of Irish Remix)"),
        new("Music/Jukebox/jukebox_06_all_but_systematic_chaos", "All But Systematic Chaos"),
        new("Music/Jukebox/jukebox_07_wartrauma", "Wartrauma"),
        new("Music/Jukebox/jukebox_08_barotrauma_main_menu", "Barotrauma — Main Menu"),
        new("Music/Jukebox/jukebox_09_crazy_dave_intro", "Crazy Dave (Intro Theme)"),
        new("Music/Jukebox/jukebox_10_one_day_of_thunder", "One Day Of Thunder"),
        new("Music/Jukebox/jukebox_11_the_great_patriotic_war", "The Great Patriotic War"),
        new("Music/Jukebox/jukebox_12_comintern_theme", "Comintern Theme"),
        new("Music/Jukebox/jukebox_13_operation_barbarossa", "Operation Barbarossa"),
        new("Music/Jukebox/jukebox_14_the_red_army", "The Red Army"),
        new("Music/Jukebox/jukebox_15_soviet_march", "Soviet March"),
        new("Music/Jukebox/jukebox_16_super_earth_national_anthem", "Super Earth National Anthem"),
        new("Music/Jukebox/jukebox_17_the_automaton_legion", "The Automaton Legion"),
        new("Music/Jukebox/jukebox_18_a_cup_of_liber_tea", "A Cup of Liber-tea"),
        new("Music/Jukebox/jukebox_19_decisions_and_consequences", "Decisions and Consequences"),
        new("Music/Jukebox/jukebox_20_colonial_anthem", "For the End is Our Glory (Colonial Anthem)"),
        new("Music/Jukebox/jukebox_21_warden_anthem", "Dreaming of the Sun (Warden Anthem)"),
        new("Music/Jukebox/jukebox_22_install_loop", "Install Loop"),
        new("Music/Jukebox/jukebox_23_terran_one", "Terran One"),
        new("Music/Jukebox/jukebox_24_factorio_trailer", "Bonus - Factorio Trailer"),
        new("Music/Jukebox/jukebox_25_glory_to_arstotzka", "Glory to Arstotzka (Main Theme)"),
    };
}
