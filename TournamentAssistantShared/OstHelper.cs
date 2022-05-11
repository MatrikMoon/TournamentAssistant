using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static TournamentAssistantShared.Constants;

/*
 * Created by Moon on 9/11/2018
 * A simple class to map "stock" levelids to their corresponding song names
 * TODO: Properly handle different map types like "OneSaber" and maps without all difficulties
 */

namespace TournamentAssistantShared
{
    [Obfuscation(Exclude = false, Feature = "+rename(mode=decodable,renPdb=true)")]
    public class OstHelper
    {
        static OstHelper()
        {
            packs = new Pack[] {
                new Pack {
                  PackID = "OstVol1",
                    PackName = "Original Soundtrack Vol. 1",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "100Bills",
                        "$100 Bills"
                      },
                      {
                        "BalearicPumping",
                        "Balearic Pumping"
                      },
                      {
                        "BeatSaber",
                        "Beat Saber"
                      },
                      {
                        "Breezer",
                        "Breezer"
                      },
                      {
                        "CommercialPumping",
                        "Commercial Pumping"
                      },
                      {
                        "CountryRounds",
                        "Country Rounds"
                      },
                      {
                        "Escape",
                        "Escape"
                      },
                      {
                        "Legend",
                        "Legend"
                      },
                      {
                        "LvlInsane",
                        "Lvl Insane"
                      },
                      {
                        "TurnMeOn",
                        "Turn Me On"
                      }
                    }
                },

                new Pack
                {
                    PackID = "OstVol2",
                    PackName = "Original Soundtrack Vol. 2",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "BeThereForYou",
                        "Be There For You"
                      },
                      {
                        "Elixia",
                        "Elixia"
                      },
                      {
                        "INeedYou",
                        "I Need You"
                      },
                      {
                        "RumNBass",
                        "Rum n' Bass"
                      },
                      {
                        "UnlimitedPower",
                        "Unlimited Power"
                      }
                    }
                },

                new Pack
                {
                    PackID = "OstVol3",
                    PackName = "Original Soundtrack Vol. 3",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "Origins",
                        "Origins"
                      },
                      {
                        "ReasonForLiving",
                        "Reason For Living"
                      },
                      {
                        "GiveALittleLove",
                        "Give a Little Love"
                      },
                      {
                        "FullCharge",
                        "Full Charge"
                      },
                      {
                        "Immortal",
                        "Immortal"
                      },
                      {
                        "BurningSands",
                        "Burning Sands"
                      }
                    }
                },

                new Pack
                {
                    PackID = "OstVol4",
                    PackName = "Original Soundtrack Vol. 4",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "IntoTheDream",
                        "Into the Dream"
                      },
                      {
                        "ItTakesMe",
                        "It Takes Me"
                      },
                      {
                        "LudicrousPlus",
                        "LUDICROUS+"
                      },
                      {
                        "SpinEternally",
                        "Spin Eternally"
                      }
                    }
                },

                new Pack
                {
                    PackID = "OstVol5",
                    PackName = "Original Soundtrack Vol. 5",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "DollarSeventyEight",
                        "$1.78"
                      },
                      {
                        "CurtainsAllNightLong",
                        "Curtains (All Night Long)"
                      },
                      {
                        "FinalBossChan",
                        "Final-Boss-Chan"
                      },
                      {
                        "Firestarter",
                        "Firestarter"
                      },
                      {
                        "IWannaBeAMachine",
                        "I Wanna Be A Machine"
                      },
                      {
                        "Magic",
                        "Magic"
                      }
                    }
                },

                new Pack
                {
                    PackID = "Extras",
                    PackName = "Extras",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "SpookyBeat",
                        "Spooky Beat"
                      },
                      {
                        "FitBeat",
                        "FitBeat"
                      },
                      {
                        "CrabRave",
                        "Crab Rave"
                      },
                      {
                        "PopStars",
                        "POP/STARS - K/DA"
                      },
                      {
                        "OneHope",
                        "One Hope"
                      },
                      {
                        "AngelVoices",
                        "Angel Voices"
                      }
                    }
                },

                new Pack
                {
                    PackID = "Camellia",
                    PackName = "Camellia Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "ExitThisEarthsAtomosphere",
                        "EXiT This Earth's Atomosphere"
                      },
                      {
                        "Ghost",
                        "Ghost"
                      },
                      {
                        "LightItUp",
                        "Light it up"
                      },
                      {
                        "Crystallized",
                        "Crystallized"
                      },
                      {
                        "CycleHit",
                        "Cycle Hit"
                      },
                      {
                        "WhatTheCat",
                        "WHAT THE CAT!?"
                      }
                    }
                },

                new Pack
                {
                    PackID = "EDM",
                    PackName = "Electronic Mixtape",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "Alone",
                        "Alone"
                      },
                      {
                        "Animals",
                        "Animals"
                      },
                      {
                        "Freestyler",
                        "Freestyler"
                      },
                      {
                        "GhostsNStuff",
                        "Ghosts 'n' Stuff"
                      },
                      {
                        "Icarus",
                        "Icarus"
                      },
                      {
                        "Sandstorm",
                        "Sandstorm"
                      },
                      {
                        "StayTheNight",
                        "Stay The Night"
                      },
                      {
                        "TheRockafellerSkank",
                        "The Rockafeller Skank"
                      },
                      {
                        "WaitingAllNight",
                        "Waiting All Night"
                      },
                      {
                        "Witchcraft",
                        "Witchcraft"
                      }
                    }
                },

                new Pack
                {
                    PackID = "FallOutBoy",
                    PackName = "Fall Out Boy",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "Centuries",
                        "Centuries"
                      },
                      {
                        "DanceDance",
                        "Dance, Dance"
                      },
                      {
                        "IDontCare",
                        "I Don't Care"
                      },
                      {
                        "Immortals",
                        "Immortals"
                      },
                      {
                        "Irresistible",
                        "Irresistible"
                      },
                      {
                        "MySongsKnow",
                        "My Songs Know What You Did In The Dark"
                      },
                      {
                        "ThisAintAScene",
                        "This Ain't A Scene, It's An Arms Race"
                      },
                      {
                        "ThnksFrThMmrs",
                        "Thnks fr th Mmrs"
                      }
                    }
                },

                new Pack
                {
                    PackID = "LadyGaga",
                    PackName = "Lady Gaga Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "Alejandro",
                        "Alejandro"
                      },
                      {
                        "BadRomance",
                        "Bad Romance"
                      },
                      {
                        "BornThisWay",
                        "Born This Way"
                      },
                      {
                        "JustDance",
                        "Just Dance"
                      },
                      {
                        "Paparazzi",
                        "Paparazzi"
                      },
                      {
                        "PokerFace",
                        "Poker Face"
                      },
                      {
                        "RainOnMe",
                        "Rain On Me"
                      },
                      {
                        "StupidLove",
                        "Stupid Love"
                      },
                      {
                        "Telephone",
                        "Telephone"
                      },
                      {
                        "TheEdgeOfGlory",
                        "The Edge Of Glory"
                      }
                    }
                },

                new Pack
                {
                    PackID = "BillieEilish",
                    PackName = "Billie Eilish",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "AllTheGoodGirlsGoToHell",
                        "all the good girls go to hell"
                      },
                      {
                        "BadGuy",
                        "bad guy"
                      },
                      {
                        "Bellyache",
                        "bellyache"
                      },
                      {
                        "BuryAFriend",
                        "bury a friend"
                      },
                      {
                        "HappierThanEver",
                        "Happier Than Ever"
                      },
                      {
                        "IDidntChangeMyNumber",
                        "I Didn't Change My Number"
                      },
                      {
                        "NDA",
                        "NDA"
                      },
                      {
                        "Oxytocin",
                        "Oxytocin"
                      },
                      {
                        "ThereforeIAm",
                        "Therefore I Am"
                      },
                      {
                        "YouShouldSeeMeInACrown",
                        "you should see me in a crown"
                      }
                    }
                },

                new Pack
                {
                    PackID = "Skrillex",
                    PackName = "Skrillex Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "Bangarang",
                        "Bangarang"
                      },
                      {
                        "Butterflies",
                        "Butterflies"
                      },
                      {
                        "DontGo",
                        "Don't Go"
                      },
                      {
                        "FirstOfTheYear",
                        "First of the Year"
                      },
                      {
                        "RaggaBomb",
                        "Ragga Bomb"
                      },
                      {
                        "RockNRoll",
                        "Rock 'n' Roll"
                      },
                      {
                        "ScaryMonstersAndNiceSprites",
                        "Scary Monsters and Nice Sprites"
                      },
                      {
                        "TheDevilsDen",
                        "The Devil's Den"
                      }
                    }
                },

                new Pack
                {
                    PackID = "Interscope",
                    PackName = "Interscope Mixtape",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "CountingStars",
                        "Counting Stars"
                      },
                      {
                        "DnaLamar",
                        "DNA."
                      },
                      {
                        "DontCha",
                        "Don't Cha"
                      },
                      {
                        "PartyRockAnthem",
                        "Party Rock Anthem"
                      },
                      {
                        "Rollin",
                        "Rollin' (Air Raid Vehicle) "
                      },
                      {
                        "Sugar",
                        "Sugar"
                      },
                      {
                        "TheSweetEscape",
                        "The Sweet Escape"
                      }
                    }
                },

                new Pack
                {
                    PackID = "BTS",
                    PackName = "BTS Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "BloodSweatAndTears",
                        "Blood Sweat & Tears"
                      },
                      {
                        "BoyWithLuv",
                        "Boy With Luv"
                      },
                      {
                        "BurningUp",
                        "Burning Up"
                      },
                      {
                        "Dionysus",
                        "Dionysus"
                      },
                      {
                        "Dna",
                        "DNA"
                      },
                      {
                        "Dope",
                        "Dope"
                      },
                      {
                        "Dynamite",
                        "Dynamite"
                      },
                      {
                        "FakeLove",
                        "FAKE LOVE"
                      },
                      {
                        "Idol",
                        "IDOL"
                      },
                      {
                        "MicDrop",
                        "MIC Drop"
                      },
                      {
                        "NotToday",
                        "Not Today"
                      },
                      {
                        "Ugh",
                        "UGH!"
                      }
                    }
                },

                new Pack
                {
                    PackID = "LinkinPark",
                    PackName = "Linkin Park Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "BleedItOut",
                        "Bleed It Out"
                      },
                      {
                        "BreakingTheHabit",
                        "Breaking the Habit"
                      },
                      {
                        "Faint",
                        "Faint"
                      },
                      {
                        "GivenUp",
                        "Given Up"
                      },
                      {
                        "InTheEnd",
                        "In the End"
                      },
                      {
                        "NewDivide",
                        "New Divide"
                      },
                      {
                        "Numb",
                        "Numb"
                      },
                      {
                        "OneStepCloser",
                        "One Step Closer"
                      },
                      {
                        "Papercut",
                        "Papercut"
                      },
                      {
                        "SomewhereIBelong",
                        "Somewhere I Belong"
                      },
                      {
                        "WhatIveDone",
                        "What I've Done"
                      }
                    }
                },

                new Pack
                {
                    PackID = "Timbaland",
                    PackName = "Timbaland Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "HasAMeaning",
                        "Has A Meaning"
                      },
                      {
                        "DumbThingz",
                        "Dumb Thingz"
                      },
                      {
                        "WhileWereYoung",
                        "While We're Young"
                      },
                      {
                        "WhatILike",
                        "What I Like"
                      },
                      {
                        "Famous",
                        "Famous"
                      }
                    }
                },

                new Pack
                {
                    PackID = "GreenDay",
                    PackName = "Green Day Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "AmericanIdiot",
                        "American Idiot"
                      },
                      {
                        "FatherOfAll",
                        "Father of All..."
                      },
                      {
                        "BoulevardOfBrokenDreams",
                        "Boulevard Of Broken Dreams"
                      },
                      {
                        "Holiday",
                        "Holiday"
                      },
                      {
                        "FireReadyAim",
                        "Fire, Ready, Aim"
                      },
                      {
                        "Minority",
                        "Minority"
                      }
                    }
                },

                new Pack
                {
                    PackID = "RocketLeague",
                    PackName = "Rocket League x Monstercat Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "Play",
                        "Play"
                      },
                      {
                        "Glide",
                        "Glide"
                      },
                      {
                        "LuvUNeedU",
                        "Luv U Need U"
                      },
                      {
                        "RockIt",
                        "Rock It"
                      },
                      {
                        "Shiawase",
                        "Shiawase"
                      },
                      {
                        "TestMe",
                        "Test Me"
                      }
                    }
                },

                new Pack
                {
                    PackID = "PanicAtTheDisco",
                    PackName = "Panic! At The Disco Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "TheGreatestShow",
                        "The Greatest Show"
                      },
                      {
                        "Victorious",
                        "Victorious"
                      },
                      {
                        "EmperorsNewClothes",
                        "Emperor's New Clothes"
                      },
                      {
                        "HighHopes",
                        "High Hopes"
                      }
                    }
                },

                new Pack
                {
                    PackID = "ImagineDragons",
                    PackName = "Imagine Dragons Music Pack",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "BadLiar",
                        "Bad Liar"
                      },
                      {
                        "Believer",
                        "Believer"
                      },
                      {
                        "Digital",
                        "Digital"
                      },
                      {
                        "ItsTime",
                        "It's Time"
                      },
                      {
                        "Machine",
                        "Machine"
                      },
                      {
                        "Natural",
                        "Natural"
                      },
                      {
                        "Radioactive",
                        "Radioactive"
                      },
                      {
                        "Thunder",
                        "Thunder"
                      },
                      {
                        "Warriors",
                        "Warriors"
                      },
                      {
                        "WhateverItTakes",
                        "Whatever It Takes"
                      }
                    }
                },

                new Pack
                {
                    PackID = "Monstercat",
                    PackName = "Monstercat Music Pack Vol. 1",
                    SongDictionary = new Dictionary<string, string> {
                      {
                        "Boundless",
                        "Boundless"
                      },
                      {
                        "EmojiVIP",
                        "Emoji VIP"
                      },
                      {
                        "Epic",
                        "EPIC"
                      },
                      {
                        "FeelingStronger",
                        "Feeling Stronger"
                      },
                      {
                        "Overkill",
                        "Overkill"
                      },
                      {
                        "Rattlesnake",
                        "Rattlesnake"
                      },
                      {
                        "Stronger",
                        "Stronger"
                      },
                      {
                        "ThisTime",
                        "This Time"
                      },
                      {
                        "TillItsOver",
                        "Till It's Over"
                      },
                      {
                        "WeWontBeAlone",
                        "We Won't Be Alone"
                      }
                    }
                },
            };

            foreach (Pack pack in packs)
            {
                allLevels = allLevels.Concat(pack.SongDictionary).ToDictionary(s => s.Key, s => s.Value);
            }
        }

        public class Pack
        {
            public string PackID { get; set; }
            public string PackName { get; set; }
            public Dictionary<string, string> SongDictionary { get; set; }
        }

        public static readonly Pack[] packs;
        public static readonly Dictionary<string, string> allLevels = new();

        //C# doesn't seem to want me to use an array of a non-primitive here.
        private static readonly int[] mainDifficulties = { (int)BeatmapDifficulty.Easy, (int)BeatmapDifficulty.Normal, (int)BeatmapDifficulty.Hard, (int)BeatmapDifficulty.Expert, (int)BeatmapDifficulty.ExpertPlus };
        private static readonly int[] angelDifficulties = { (int)BeatmapDifficulty.Hard, (int)BeatmapDifficulty.Expert, (int)BeatmapDifficulty.ExpertPlus };
        private static readonly int[] oneSaberDifficulties = { (int)BeatmapDifficulty.Expert };
        private static readonly int[] noArrowsDifficulties = { (int)BeatmapDifficulty.Expert };

        public static string GetOstSongNameFromLevelId(string hash)
        {
            hash = hash.EndsWith("OneSaber") ? hash.Substring(0, hash.IndexOf("OneSaber")) : hash;
            hash = hash.EndsWith("NoArrows") ? hash.Substring(0, hash.IndexOf("NoArrows")) : hash;
            return allLevels[hash];
        }

        public static BeatmapDifficulty[] GetDifficultiesFromLevelId(string levelId)
        {
            if (IsOst(levelId))
            {
                if (levelId.Contains("OneSaber")) return oneSaberDifficulties.Select(x => (BeatmapDifficulty)x).ToArray();
                else if (levelId.Contains("NoArrows")) return noArrowsDifficulties.Select(x => (BeatmapDifficulty)x).ToArray();
                else if (levelId != "AngelVoices") return mainDifficulties.Select(x => (BeatmapDifficulty)x).ToArray();
                else return angelDifficulties.Select(x => (BeatmapDifficulty)x).ToArray();
            }
            return null;
        }

        public static bool IsOst(string levelId)
        {
            levelId = levelId.EndsWith("OneSaber") ? levelId.Substring(0, levelId.IndexOf("OneSaber")) : levelId;
            levelId = levelId.EndsWith("NoArrows") ? levelId.Substring(0, levelId.IndexOf("NoArrows")) : levelId;
            return packs.Any(x => x.SongDictionary.ContainsKey(levelId));
        }
    }
}
