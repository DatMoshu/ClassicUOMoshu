using System;
using System.Collections.Generic;
// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Game
{
    internal static class SpeechRecognitionStrings
    {
        public static readonly Dictionary<string, int> NumberWords = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = 0,
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3,
            ["four"] = 4,
            ["five"] = 5,
            ["six"] = 6,
            ["seven"] = 7,
            ["eight"] = 8,
            ["nine"] = 9
        };

        public static readonly string[] Greetings = new string[]
        {
                    "Hail!", "Well met!", "Greetings, good sir!", "How dost thou?", "What ho!", "Good morrow!", "Salutations, traveler!", "Yo, varlet!", "Hiya, knave!",
                    "Ahoy, matey!", "Bonjour, noble one!", "Hola, fair one!", "Ciao, good sir!", "Konnichiwa, traveler!", "Namaste, noble one!", "Shalom, fair one!", "G'day, mate!", "Heya, knave!", "Sup, varlet!",
                    "Wassup, knave?", "Howdy-do, good sir!", "Hey there, varlet!", "Hi-ya, knave!", "Greetings, wanderer!", "Hello, friend!", "Hey, buddy!", "Hi, pal!", "Hey, mate!",
                    "Hello, sunshine!", "Hi, champ!", "Hey, superstar!", "Greetings, earthling!", "Hello, world!", "Hi, hero!", "Hey, legend!", "Hello, genius!", "Hi, smarty!",
                    "Hey, brainiac!", "Greetings, adventurer!", "Hello, explorer!", "Hi, wanderer!", "Hey, seeker!", "Hello, voyager!", "Hi, discoverer!", "Hey, pathfinder!", "Hello, trailblazer!", "Hi, pioneer!"
        };

        public static readonly string[] Farewells = new string[]
        {
                    "Fare thee well!", "See thee anon!", "Parting is such sweet sorrow!", "Godspeed!", "Take care, noble one!", "Catch thee later!", "Adieu!", "Ciao, fair one!", "Sayonara, good sir!", "Au revoir, noble one!",
                    "Later, varlet!", "Peace out, knave!", "Toodles, good sir!", "Cheerio, fair one!", "Bye-bye, noble one!", "So long, traveler!", "Ta-ta, knave!", "Catch thee on the flip side!", "See thee soon, good sir!", "Take it easy, noble one!",
                    "Stay safe, fair one!", "Keep in touch, good sir!", "Godspeed, noble one!", "Until next time, traveler!", "See thee around, knave!", "Stay cool, fair one!", "Stay awesome, good sir!", "Don't be a stranger, noble one!", "Catch thee later, alligator!", "After a while, crocodile!",
                    "Hasta la vista, baby!", "Smell thee later, knave!", "Bye for now, good sir!", "Take it sleazy, fair one!", "Stay classy, noble one!", "Catch thee on the rebound, knave!", "See thee in the funny papers, good sir!", "Don't get lost, fair one!", "Don't do anything I wouldn't do, noble one!",
                    "May the force be with thee!", "Live long and prosper!", "Beam me up, Scotty!", "Hasta la vista, baby!", "I'll be back!", "Keep on truckin'!", "Rock on!", "Party on, dude!", "Catch thee on the flip side!", "Stay golden!"
        };

        public static readonly string[] Acknowledgements = new string[]
        {
                    "Dost thou say?", "Verily, I hear thee!", "Thy words are noted!", "Understood, good sir!", "Aye, I hear thee!",
                    "Affirmative, milord!", "Thy message is received!", "Acknowledged, my liege!", "Noted, fair one!", "Confirmed, noble one!",
                    "I have heard thee!", "Loud and clear, my lord!", "I comprehend thy words!", "As thou wishest!", "Certainly, my liege!",
                    "Of course, good sir!", "No problem, milady!", "Alright, my lord!", "I hear thee, noble one!", "Loud and clear, milady!",
                    "I got thee, fair one!", "I hear thy command!", "I catch thy drift!", "I understand thy words!", "I acknowledge thy command!",
                    "I recognize thy voice!", "I perceive thy words!", "I grasp thy meaning!", "I comprehend thy command!", "I discern thy intent!",
                    "I register thy words!", "I note thy command!", "I observe thy speech!", "I detect thy voice!", "I pick up thy words!",
                    "I catch thy meaning!", "I get thy point!", "I follow thy command!", "Thy words are clear!", "I heed thy words!",
                    "Thy command is noted!", "I hear thee, my liege!", "I understand thee, noble one!", "I acknowledge thee, fair one!",
                    "I recognize thee, good sir!", "I perceive thee!", "I grasp thy command, my lord!", "I comprehend thee, noble one!",
                    "I discern thy words, my liege!"
        };

        public static readonly string[] StartListeningCommands = new string[]
        {
            "avatar start listening", "avatar listen", "what is up", "enable speech", "enable speech recognition", "speech on",
            "speech recognition on", "hey avatar start listening", "hey avatar listen", "hey avatar", "avatar can you hear me",
            "avatar are you there", "avatar wake up", "avatar activate", "avatar start", "avatar begin", "avatar ready",
            "avatar engage", "avatar on", "avatar go", "avatar initiate", "avatar commence", "avatar open ears", "avatar pay attention",
            "avatar focus", "avatar alert", "avatar listen up", "avatar hear me", "avatar respond", "avatar acknowledge",
            "avatar ready up", "avatar prepare", "avatar get ready", "avatar start speech", "avatar start recognition",
            "avatar start speech recognition", "avatar enable", "avatar enable recognition", "avatar enable speech recognition",
            "start listening", "listen", "start speech", "start recognition", "start speech recognition", "enable",
            "enable recognition", "enable speech recognition", "speech true", "speech on", "can I have your ear", "lend me your ear",
            "hear me", "pay attention", "focus", "alert", "listen up", "respond", "acknowledge", "ready up", "prepare",
            "get ready", "commence", "engage", "begin", "go", "ready", "activate", "wake up", "are you there", "can you hear me"
        };

        public static readonly Dictionary<string, List<string>> PlayerQuestions = new Dictionary<string, List<string>>
        {
            { "say I am well, thank you!", new List<string> { "how are you", "how do you do", "how's it going", "how are you doing" } },
            { "say The current time is [time].", new List<string> { "do you have the time", "what time is it", "can you tell me the time" } },
            { "say Who's there?", new List<string> { "knock knock", "who's there" } },
            { "say I am your loyal avatar.", new List<string> { "what is your name", "who are you", "tell me your name" } },
            { "say We are in the land of Britannia.", new List<string> { "where are we", "what is this place", "where am I" } },
            { "say My quest is to assist you, noble one.", new List<string> { "what is your quest", "what is your mission", "what are you doing" } },
            { "say Why don't skeletons fight each other? They don't have the guts.", new List<string> { "do you know any jokes", "tell me a joke", "make me laugh" } },
            { "say The meaning of life is to find your own path and follow it.", new List<string> { "what is the meaning of life", "why are we here", "what is the purpose of life" } },
            { "say I am here to help you with whatever you need.", new List<string> { "can you help me", "I need assistance", "help me" } },
            { "say My favorite color is the color of your victory.", new List<string> { "what is your favorite color", "do you have a favorite color", "what color do you like" } },
            { "say Opening your inventory.", new List<string> { "open your inventory", "inventory", "what is in your bag", "open bag", "bag", "show inventory", "show bag", "what do I have" } },
            { "say Opening your paperdoll.", new List<string> { "open paperdoll", "show doll", "open doll", "show player", "open player", "show character", "open character", "paperdoll" } },
            { "say Opening your quests.", new List<string> { "open quest", "what are my quests", "bring up quests", "show quests", "open quests", "show my quests", "quest" } }
        };

        public static readonly string[] StopListeningCommands = new string[]
        {
            "avatar stop listening", "avatar stop", "avatar halt", "avatar cease", "avatar quiet", "avatar silence", "avatar mute",
            "stop listening", "stop", "halt", "cease", "quiet", "silence", "mute", "disable speech", "disable speech recognition",
            "speech off", "speech recognition off", "hey avatar stop listening", "hey avatar stop", "hey avatar halt", "hey avatar cease",
            "hey avatar quiet", "hey avatar silence", "hey avatar mute", "avatar can you stop", "avatar are you done", "avatar go to sleep",
            "avatar deactivate", "avatar end", "avatar finish", "avatar shut down", "avatar close ears", "avatar ignore", "avatar standby",
            "avatar sleep", "avatar rest", "avatar pause", "avatar suspend", "avatar terminate", "avatar shut up", "avatar be quiet",
            "avatar be silent", "avatar turn off", "avatar power down", "avatar stop speech", "avatar stop recognition", "avatar stop speech recognition",
            "avatar disable", "avatar disable recognition", "avatar disable speech recognition", "stop speech", "stop recognition", "stop speech recognition",
            "disable", "disable recognition", "disable speech recognition", "speech false", "speech off", "can you be quiet", "can you be silent",
            "stop hearing me", "stop paying attention", "stop focusing", "stop alert", "stop listening up", "stop responding", "stop acknowledging",
            "stand down", "relax", "chill", "take a break", "rest", "pause", "suspend", "terminate", "shut down", "power down", "deactivate", "sleep",
            "go to sleep", "are you done", "can you stop"
        };
    }
}