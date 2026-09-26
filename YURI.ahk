; =============================================================================
;  YURI - the last AutoHotkey build
;  =============================================================================
;  This file exists for one reason: to carry the people still running the .ahk
;  hub over to the built application, which is where every version after this
;  one lives.
;
;  It is what the old hub's self-updater expects to find. That updater reads
;  the APP_VERSION line below out of the first 8 KB of this file, installs the
;  file if the version beats its own, checks that AutoHotkey can parse it, then
;  runs it. So an old copy updates to this, and this puts YURI.exe down and
;  starts it.
;
;  It always fetches the NEWEST release (releases/latest), so it does not need
;  editing for each new version. APP_VERSION is only the number the old hub's
;  "NEWER VERSION FOUND" card shows; bump it with each release if you want
;  that card to show the current one.
;
;  Nothing here is clever, on purpose. It has one job and it must not fail in a
;  way that leaves somebody stranded on the old build: every step is wrapped,
;  and if the download cannot be had, the user is told exactly where to get it
;  by hand.
; =============================================================================

;@Ahk2Exe-SetVersion 2.1.0.0
#Requires AutoHotkey v2.0
#SingleInstance Force
Persistent false

; The version the old updater compares against. It must beat 1.0.3, and it must
; sit inside the first 8 KB of the file, which it does.
global APP_VERSION := "2.1.0"

global EXE_URL   := "https://github.com/lIIusionator/YuriHub/releases/latest/download/YURI.exe"
global PAGE_URL  := "https://github.com/lIIusionator/YuriHub/releases/latest"
global DEST_DIR  := A_ScriptDir
global DEST_EXE  := DEST_DIR "\YURI.exe"

Main()

Main() {
    ; Already installed beside this file and running? Then there is nothing to
    ; do but step aside - a built YURI updates itself.
    if FileExist(DEST_EXE) && Running(DEST_EXE) {
        ExitApp
    }

    ; Already installed, just not running: start it - but only if it is the
    ; newest. An older YURI.exe left beside this file used to be started as it
    ; was, so the update stopped at whatever version happened to be there.
    latest := LatestVersion()
    have := (FileExist(DEST_EXE) && FileGetSizeSafe(DEST_EXE) > 5000000) ? ExeVersion(DEST_EXE) : ""
    if (have != "" && (latest = "" || VerCompare(have, latest) >= 0)) {
        if Launch(DEST_EXE)
            ExitApp
    }

    target := (latest != "") ? " v" latest : ""
    if (have != "")
        ask := "An older YURI.exe (v" ShortVer(have) ") is next to this file.`n`nDownload YURI" target " and start it?"
    else
        ask := "YURI is no longer an AutoHotkey script.`n`n"
            . "Everything after v1.0.3 is a built application - the same hub, "
            . "faster, with the fast flag manager, the cursor module, the script "
            . "hub and the rest.`n`n"
            . "Download YURI" target " now and start it?"
    if MsgBox(ask, "YURI - one last update", "OKCancel Icon!") = "Cancel" {
        Bye("You can download it any time from:`n" PAGE_URL)
    }

    tmp := A_Temp "\YURI_" A_TickCount ".exe"
    ok := false
    try {
        Download(EXE_URL, tmp)
        ok := FileExist(tmp) && FileGetSizeSafe(tmp) > 5000000   ; a 404 page is a few hundred bytes
    }
    if !ok {
        try FileDelete(tmp)
        Bye("The download did not come through.`n`n"
          . "Get YURI.exe by hand from:`n" PAGE_URL "`n`n"
          . "Put it wherever you like and run it - it needs nothing beside it.")
    }

    ; Put it beside this script if that is writable; otherwise leave it where it
    ; downloaded and run it from there.
    dest := DEST_EXE
    placed := false
    try {
        FileMove(tmp, dest, 1)
        placed := FileExist(dest) ? true : false
    }
    if !placed {
        dest := tmp
    }

    if !Launch(dest) {
        Bye("YURI.exe was downloaded to:`n" dest "`n`nStart it from there.")
    }

    ; It is up. Retire this script: the old hub's updater kept a copy of the
    ; build it replaced, and neither is wanted now.
    try FileDelete(A_ScriptFullPath ".old")
    ExitApp
}

; The newest release's version, from where /releases/latest lands
; (.../releases/tag/v2.1.0). No API call, so no rate limit. "" if it cannot be
; had - then an existing YURI.exe is trusted as it is.
LatestVersion() {
    try {
        req := ComObject("WinHttp.WinHttpRequest.5.1")
        req.SetTimeouts(5000, 5000, 10000, 15000)
        req.Open("GET", PAGE_URL, false)
        req.SetRequestHeader("User-Agent", "YURI")
        req.Send()
        url := ""
        try url := req.Option[1]                    ; WinHttpRequestOption_URL: the address after the redirect
        if RegExMatch(url, "i)/tag/v?(\d+(?:\.\d+)*)", &m)
            return m[1]
        if RegExMatch(req.ResponseText, "i)/releases/tag/v?(\d+(?:\.\d+)*)", &m)   ; the page itself, if the address could not be read
            return m[1]
    }
    return ""
}

; The version stamped on a YURI.exe ("2.0.1.0"), or "" if it has none.
ExeVersion(path) {
    try return FileGetVersion(path)
    return ""
}

; "2.0.1.0" -> "2.0.1", for the message.
ShortVer(v) {
    return RegExReplace(v, "\.0$", "")
}

Launch(path) {
    try {
        Run('"' path '"', DirOf(path))
        return true
    }
    return false
}

Running(path) {
    try {
        for p in ComObjGet("winmgmts:").ExecQuery("SELECT ExecutablePath FROM Win32_Process WHERE Name='YURI.exe'")
            if (p.ExecutablePath != "" && StrLower(p.ExecutablePath) = StrLower(path))
                return true
    }
    return false
}

FileGetSizeSafe(path) {
    try return FileGetSize(path)
    return 0
}

DirOf(path) {
    SplitPath(path, , &dir)
    return dir
}

Bye(msg) {
    MsgBox(msg, "YURI", "Iconi")
    ExitApp
}
