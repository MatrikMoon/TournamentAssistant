export enum BeatmapDifficulty {
    Easy,
    Normal,
    Hard,
    Expert,
    ExpertPlus
}

export interface Song {
    levelId: string,
    levelName: string
}

export interface Pack {
    packId: string;
    packName: string;
    songs: Song[];
}

const packs: Pack[] = [
    {
        packId: "OstVol1",
        packName: "Original Soundtrack Vol. 1",
        songs: [
            { levelId: "100Bills", levelName: "$100 Bills" },
            { levelId: "BalearicPumping", levelName: "Balearic Pumping" },
            { levelId: "BeatSaber", levelName: "Beat Saber" },
            { levelId: "Breezer", levelName: "Breezer" },
            { levelId: "CommercialPumping", levelName: "Commercial Pumping" },
            { levelId: "CountryRounds", levelName: "Country Rounds" },
            { levelId: "Escape", levelName: "Escape" },
            { levelId: "Legend", levelName: "Legend" },
            { levelId: "LvlInsane", levelName: "Lvl Insane" },
            { levelId: "TurnMeOn", levelName: "Turn Me On" }
        ]
    },
    {
        packId: "OstVol2",
        packName: "Original Soundtrack Vol. 2",
        songs: [
            { levelId: "BeThereForYou", levelName: "Be There For You" },
            { levelId: "Elixia", levelName: "Elixia" },
            { levelId: "INeedYou", levelName: "I Need You" },
            { levelId: "RumNBass", levelName: "Rum n' Bass" },
            { levelId: "UnlimitedPower", levelName: "Unlimited Power" }
        ]
    },
    {
        packId: "OstVol3",
        packName: "Original Soundtrack Vol. 3",
        songs: [
            { levelId: "Origins", levelName: "Origins" },
            { levelId: "ReasonForLiving", levelName: "Reason For Living" },
            { levelId: "GiveALittleLove", levelName: "Give a Little Love" },
            { levelId: "FullCharge", levelName: "Full Charge" },
            { levelId: "Immortal", levelName: "Immortal" },
            { levelId: "BurningSands", levelName: "Burning Sands" }
        ]
    },
    {
        packId: "OstVol4",
        packName: "Original Soundtrack Vol. 4",
        songs: [
            { levelId: "IntoTheDream", levelName: "Into the Dream" },
            { levelId: "ItTakesMe", levelName: "It Takes Me" },
            { levelId: "LudicrousPlus", levelName: "LUDICROUS+" },
            { levelId: "SpinEternally", levelName: "Spin Eternally" }
        ]
    },
    {
        packId: "OstVol5",
        packName: "Original Soundtrack Vol. 5",
        songs: [
            { levelId: "DollarSeventyEight", levelName: "$1.78" },
            { levelId: "CurtainsAllNightLong", levelName: "Curtains (All Night Long)" },
            { levelId: "FinalBossChan", levelName: "Final-Boss-Chan" },
            { levelId: "Firestarter", levelName: "Firestarter" },
            { levelId: "IWannaBeAMachine", levelName: "I Wanna Be A Machine" },
            { levelId: "Magic", levelName: "Magic" }
        ]
    },
    {
        packId: "Extras",
        packName: "Extras",
        songs: [
            { levelId: "SpookyBeat", levelName: "Spooky Beat" },
            { levelId: "FitBeat", levelName: "FitBeat" },
            { levelId: "CrabRave", levelName: "Crab Rave" },
            { levelId: "PopStars", levelName: "POP/STARS - K/DA" },
            { levelId: "OneHope", levelName: "One Hope" },
            { levelId: "AngelVoices", levelName: "Angel Voices" }
        ]
    },
    {
        packId: "Camellia",
        packName: "Camellia Music Pack",
        songs: [
            { levelId: "ExitThisEarthsAtomosphere", levelName: "EXiT This Earth's Atomosphere" },
            { levelId: "Ghost", levelName: "Ghost" },
            { levelId: "LightItUp", levelName: "Light it up" },
            { levelId: "Crystallized", levelName: "Crystallized" },
            { levelId: "CycleHit", levelName: "Cycle Hit" },
            { levelId: "WhatTheCat", levelName: "WHAT THE CAT!?" }
        ]
    },
    {
        packId: "EDM",
        packName: "Electronic Mixtape",
        songs: [
            { levelId: "Alone", levelName: "Alone" },
            { levelId: "Animals", levelName: "Animals" },
            { levelId: "Freestyler", levelName: "Freestyler" },
            { levelId: "GhostsNStuff", levelName: "Ghosts 'n' Stuff" },
            { levelId: "Icarus", levelName: "Icarus" },
            { levelId: "Sandstorm", levelName: "Sandstorm" },
            { levelId: "StayTheNight", levelName: "Stay The Night" },
            { levelId: "TheRockafellerSkank", levelName: "The Rockafeller Skank" },
            { levelId: "WaitingAllNight", levelName: "Waiting All Night" },
            { levelId: "Witchcraft", levelName: "Witchcraft" }
        ]
    },
    {
        packId: "FallOutBoy",
        packName: "Fall Out Boy",
        songs: [
            { levelId: "Centuries", levelName: "Centuries" },
            { levelId: "DanceDance", levelName: "Dance, Dance" },
            { levelId: "IDontCare", levelName: "I Don't Care" },
            { levelId: "Immortals", levelName: "Immortals" },
            { levelId: "Irresistible", levelName: "Irresistible" },
            { levelId: "MySongsKnow", levelName: "My Songs Know What You Did In The Dark" },
            { levelId: "ThisAintAScene", levelName: "This Ain't A Scene, It's An Arms Race" },
            { levelId: "ThnksFrThMmrs", levelName: "Thnks fr th Mmrs" }
        ]
    },
    {
        packId: "LadyGaga",
        packName: "Lady Gaga Music Pack",
        songs: [
            { levelId: "Alejandro", levelName: "Alejandro" },
            { levelId: "BadRomance", levelName: "Bad Romance" },
            { levelId: "BornThisWay", levelName: "Born This Way" },
            { levelId: "JustDance", levelName: "Just Dance" },
            { levelId: "Paparazzi", levelName: "Paparazzi" },
            { levelId: "PokerFace", levelName: "Poker Face" },
            { levelId: "RainOnMe", levelName: "Rain On Me" },
            { levelId: "StupidLove", levelName: "Stupid Love" },
            { levelId: "Telephone", levelName: "Telephone" },
            { levelId: "TheEdgeOfGlory", levelName: "The Edge Of Glory" }
        ]
    },
    {
        packId: "BillieEilish",
        packName: "Billie Eilish",
        songs: [
            { levelId: "AllTheGoodGirlsGoToHell", levelName: "all the good girls go to hell" },
            { levelId: "BadGuy", levelName: "bad guy" },
            { levelId: "Bellyache", levelName: "bellyache" },
            { levelId: "BuryAFriend", levelName: "bury a friend" },
            { levelId: "HappierThanEver", levelName: "Happier Than Ever" },
            { levelId: "IDidntChangeMyNumber", levelName: "I Didn't Change My Number" },
            { levelId: "NDA", levelName: "NDA" },
            { levelId: "Oxytocin", levelName: "Oxytocin" },
            { levelId: "ThereforeIAm", levelName: "Therefore I Am" },
            { levelId: "YouShouldSeeMeInACrown", levelName: "you should see me in a crown" },
        ],
    },
    {
        packId: "Skrillex",
        packName: "Skrillex Music Pack",
        songs: [
            { levelId: "Bangarang", levelName: "Bangarang" },
            { levelId: "Butterflies", levelName: "Butterflies" },
            { levelId: "DontGo", levelName: "Don't Go" },
            { levelId: "FirstOfTheYear", levelName: "First of the Year" },
            { levelId: "RaggaBomb", levelName: "Ragga Bomb" },
            { levelId: "RockNRoll", levelName: "Rock 'n' Roll" },
            { levelId: "ScaryMonstersAndNiceSprites", levelName: "Scary Monsters and Nice Sprites" },
            { levelId: "TheDevilsDen", levelName: "The Devil's Den" },
        ],
    },
    {
        packId: "Interscope",
        packName: "Interscope Mixtape",
        songs: [
            { levelId: "CountingStars", levelName: "Counting Stars" },
            { levelId: "DnaLamar", levelName: "DNA." },
            { levelId: "DontCha", levelName: "Don't Cha" },
            { levelId: "PartyRockAnthem", levelName: "Party Rock Anthem" },
            { levelId: "Rollin", levelName: "Rollin' (Air Raid Vehicle)" },
            { levelId: "Sugar", levelName: "Sugar" },
            { levelId: "TheSweetEscape", levelName: "The Sweet Escape" },
        ],
    },
    {
        packId: "BTS",
        packName: "BTS Music Pack",
        songs: [
            { levelId: "BloodSweatAndTears", levelName: "Blood Sweat & Tears" },
            { levelId: "BoyWithLuv", levelName: "Boy With Luv" },
            { levelId: "BurningUp", levelName: "Burning Up" },
            { levelId: "Dionysus", levelName: "Dionysus" },
            { levelId: "Dna", levelName: "DNA" },
            { levelId: "Dope", levelName: "Dope" },
            { levelId: "Dynamite", levelName: "Dynamite" },
            { levelId: "FakeLove", levelName: "FAKE LOVE" },
            { levelId: "Idol", levelName: "IDOL" },
            { levelId: "MicDrop", levelName: "MIC Drop" },
            { levelId: "NotToday", levelName: "Not Today" },
            { levelId: "Ugh", levelName: "UGH!" },
        ],
    },
    {
        packId: "LinkinPark",
        packName: "Linkin Park Music Pack",
        songs: [
            { levelId: "BleedItOut", levelName: "Bleed It Out" },
            { levelId: "BreakingTheHabit", levelName: "Breaking the Habit" },
            { levelId: "Faint", levelName: "Faint" },
            { levelId: "GivenUp", levelName: "Given Up" },
            { levelId: "InTheEnd", levelName: "In the End" },
            { levelId: "NewDivide", levelName: "New Divide" },
            { levelId: "Numb", levelName: "Numb" },
            { levelId: "OneStepCloser", levelName: "One Step Closer" },
            { levelId: "Papercut", levelName: "Papercut" },
            { levelId: "SomewhereIBelong", levelName: "Somewhere I Belong" },
            { levelId: "WhatIveDone", levelName: "What I've Done" },
        ],
    },
    {
        packId: "Timbaland",
        packName: "Timbaland Music Pack",
        songs: [
            { levelId: "HasAMeaning", levelName: "Has A Meaning" },
            { levelId: "DumbThingz", levelName: "Dumb Thingz" },
            { levelId: "WhileWereYoung", levelName: "While We're Young" },
            { levelId: "WhatILike", levelName: "What I Like" },
            { levelId: "Famous", levelName: "Famous" }
        ]
    },
    {
        packId: "GreenDay",
        packName: "Green Day Music Pack",
        songs: [
            { levelId: "AmericanIdiot", levelName: "American Idiot" },
            { levelId: "FatherOfAll", levelName: "Father of All..." },
            { levelId: "BoulevardOfBrokenDreams", levelName: "Boulevard Of Broken Dreams" },
            { levelId: "Holiday", levelName: "Holiday" },
            { levelId: "FireReadyAim", levelName: "Fire, Ready, Aim" },
            { levelId: "Minority", levelName: "Minority" }
        ]
    },
    {
        packId: "RocketLeague",
        packName: "Rocket League x Monstercat Music Pack",
        songs: [
            { levelId: "Play", levelName: "Play" },
            { levelId: "Glide", levelName: "Glide" },
            { levelId: "LuvUNeedU", levelName: "Luv U Need U" },
            { levelId: "RockIt", levelName: "Rock It" },
            { levelId: "Shiawase", levelName: "Shiawase" },
            { levelId: "TestMe", levelName: "Test Me" }
        ]
    },
    {
        packId: "PanicAtTheDisco",
        packName: "Panic! At The Disco Music Pack",
        songs: [
            { levelId: "TheGreatestShow", levelName: "The Greatest Show" },
            { levelId: "Victorious", levelName: "Victorious" },
            { levelId: "EmperorsNewClothes", levelName: "Emperor's New Clothes" },
            { levelId: "HighHopes", levelName: "High Hopes" }
        ]
    },
    {
        packId: "ImagineDragons",
        packName: "Imagine Dragons Music Pack",
        songs: [
            { levelId: "BadLiar", levelName: "Bad Liar" },
            { levelId: "Believer", levelName: "Believer" },
            { levelId: "Digital", levelName: "Digital" },
            { levelId: "ItsTime", levelName: "It's Time" },
            { levelId: "Machine", levelName: "Machine" },
            { levelId: "Natural", levelName: "Natural" },
            { levelId: "Radioactive", levelName: "Radioactive" },
            { levelId: "Thunder", levelName: "Thunder" },
            { levelId: "Warriors", levelName: "Warriors" },
            { levelId: "WhateverItTakes", levelName: "Whatever It Takes" }
        ]
    },
    {
        packId: "Monstercat",
        packName: "Monstercat Music Pack Vol. 1",
        songs: [
            { levelId: "Boundless", levelName: "Boundless" },
            { levelId: "EmojiVIP", levelName: "Emoji VIP" },
            { levelId: "Epic", levelName: "EPIC" },
            { levelId: "FeelingStronger", levelName: "Feeling Stronger" },
            { levelId: "Overkill", levelName: "Overkill" },
            { levelId: "Rattlesnake", levelName: "Rattlesnake" },
            { levelId: "Stronger", levelName: "Stronger" },
            { levelId: "ThisTime", levelName: "This Time" },
            { levelId: "TillItsOver", levelName: "Till It's Over" },
            { levelId: "WeWontBeAlone", levelName: "We Won't Be Alone" }
        ]
    }
];

export function getPacks(): Pack[] {
    return packs;
}

let allLevels: Song[] = [];

export function getAllLevels(): Song[] {
    if (allLevels.length > 0) {
        return allLevels;
    }

    for (const pack of packs) {
        allLevels = [...allLevels, ...pack.songs];
    }

    return allLevels;
};

const mainDifficulties = [BeatmapDifficulty.Easy, BeatmapDifficulty.Normal, BeatmapDifficulty.Hard, BeatmapDifficulty.Expert, BeatmapDifficulty.ExpertPlus];
const angelDifficulties = [BeatmapDifficulty.Hard, BeatmapDifficulty.Expert, BeatmapDifficulty.ExpertPlus];
const oneSaberDifficulties = [BeatmapDifficulty.Expert];
const noArrowsDifficulties = [BeatmapDifficulty.Expert];

function getOstSongNameFromLevelId(hash: string): string {
    hash = hash.endsWith("OneSaber") ? hash.slice(0, hash.indexOf("OneSaber")) : hash;
    hash = hash.endsWith("NoArrows") ? hash.slice(0, hash.indexOf("NoArrows")) : hash;
    return allLevels.find(x => x.levelId === hash)!.levelName;
}

export function getDifficultiesFromLevelId(levelId: string): BeatmapDifficulty[] {
    if (levelId.includes("OneSaber")) return oneSaberDifficulties.map(x => x as BeatmapDifficulty);
    else if (levelId.includes("NoArrows")) return noArrowsDifficulties.map(x => x as BeatmapDifficulty);
    else if (levelId !== "AngelVoices") return mainDifficulties.map(x => x as BeatmapDifficulty);
    else return angelDifficulties.map(x => x as BeatmapDifficulty);
}

export function isOst(levelId: string): boolean {
    levelId = levelId.endsWith("OneSaber") ? levelId.slice(0, levelId.indexOf("OneSaber")) : levelId;
    levelId = levelId.endsWith("NoArrows") ? levelId.slice(0, levelId.indexOf("NoArrows")) : levelId;
    return packs.some(x => x.songs.find(x => x.levelId === levelId));
}