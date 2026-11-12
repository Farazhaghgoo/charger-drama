using System.Collections.Generic;

namespace ChargerDrama
{
    // One kind of reaction the app can play (laugh, nooo, low battery, ...).
    // Audio files for it are named "<Key>*.<ext>" inside a voice folder, e.g. "laugh-1.mp3".
    internal sealed class VoiceCategory
    {
        public string Key { get; private set; }
        public string Title { get; private set; }
        public string Example { get; private set; }

        public VoiceCategory(string key, string title, string example)
        {
            Key = key;
            Title = title;
            Example = example;
        }

        private static readonly List<VoiceCategory> all = new List<VoiceCategory>
        {
            new VoiceCategory("laugh",     "Charger plugged in",                  "Mwahahaha! Sweet, sweet electricity!"),
            new VoiceCategory("nooo",      "Charger pulled out",                  "Nooooo! No no no no! Please... no!"),
            new VoiceCategory("low20",     "Battery down to 20%",                 "I'm... getting... weak..."),
            new VoiceCategory("low5",      "Battery down to 5%",                  "Tell my files... I loved them..."),
            new VoiceCategory("annoyed",   "Plugged and unplugged too many times", "Are you PLAYING with me?!"),
            new VoiceCategory("relief",    "Plugged in after a long time on battery", "FINALLY! Do you know how long I waited?!"),
            new VoiceCategory("usb-in",    "USB stick plugged in",                "Ooh! What's that? Is it for me?"),
            new VoiceCategory("usb-out",   "USB stick removed",                   "Hey! Bring it back!"),
            new VoiceCategory("mouse-in",  "Mouse connected",                     "A mouse! Hello, little friend!"),
            new VoiceCategory("mouse-out", "Mouse disconnected",                  "Where did my mouse go?"),
            new VoiceCategory("poke",      "You clicked the mascot",              "Hee hee! That tickles!"),
        };

        public static IList<VoiceCategory> All { get { return all.AsReadOnly(); } }

        public static VoiceCategory Find(string key)
        {
            foreach (var c in all) if (c.Key == key) return c;
            return null;
        }
    }
}
