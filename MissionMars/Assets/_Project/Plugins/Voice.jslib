// NOVA's voice: the speech voice built into the browser (Web Speech API). Free, no key, nothing to download.
mergeInto(LibraryManager.library, {
  MM_Speak: function (textPtr, rate, pitch, volume) {
    var text = UTF8ToString(textPtr);
    if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return;
    try {
      var s = window.speechSynthesis;
      s.cancel();
      var u = new SpeechSynthesisUtterance(text);
      var voices = s.getVoices() || [];
      // a clear English voice: the nicer online ones first, then any English voice
      var prefs = ["Google US English", "Microsoft Aria", "Microsoft Jenny", "Samantha", "Google UK English Female", "Microsoft Zira", "Karen"];
      var pick = null;
      for (var i = 0; i < prefs.length && !pick; i++)
        for (var j = 0; j < voices.length; j++) if (voices[j].name.indexOf(prefs[i]) >= 0) { pick = voices[j]; break; }
      if (!pick) for (var k = 0; k < voices.length; k++) if (/^en[-_]/i.test(voices[k].lang)) { pick = voices[k]; break; }
      if (pick) u.voice = pick;
      u.lang = pick ? pick.lang : "en-US";
      u.rate = rate; u.pitch = pitch; u.volume = volume;
      s.speak(u);
    } catch (e) { }
  },
  MM_StopSpeak: function () {
    try { if (window.speechSynthesis) window.speechSynthesis.cancel(); } catch (e) { }
  },
  MM_SpeakReady: function () {
    // load the voice list early (Chrome fills it a moment after the page opens)
    try { if (window.speechSynthesis) { window.speechSynthesis.getVoices(); return 1; } } catch (e) { }
    return 0;
  }
});
