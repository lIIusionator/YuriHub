; AutoHotkey v2.0 Example - Fetching keys from server
; This shows how YURI_test.ahk can fetch keys from the running server

#Requires AutoHotkey v2.0

; ============================================
; CONFIGURATION
; ============================================
KEYS_SERVER_URL := "http://localhost:3000"
KEYS_ENDPOINT := KEYS_SERVER_URL . "/api/keys"

; ============================================
; FUNCTION: Fetch keys from server
; ============================================
FetchKeysFromServer() {
    try {
        ; Create WinHttpRequest object
        http := ComObject("WinHttp.WinHttpRequest.5.1")
        
        ; Open connection to server
        http.Open("GET", KEYS_ENDPOINT, false)
        
        ; Set headers
        http.SetRequestHeader("Content-Type", "application/json")
        
        ; Send request
        http.Send()
        
        ; Get response
        response := http.ResponseText
        
        ; Parse JSON response
        keys := ParseJSON(response)
        
        MsgBox("Keys loaded successfully! Total keys: " . keys.keys.Length)
        return keys
    }
    catch as err {
        MsgBox("Error fetching keys: " . err.Message)
        return false
    }
}

; ============================================
; FUNCTION: Parse JSON response
; ============================================
ParseJSON(jsonString) {
    ; Using built-in JSON parsing (AHK v2.0+)
    try {
        obj := JSON.Parse(jsonString)
        return obj
    }
    catch as err {
        MsgBox("JSON Parse error: " . err.Message)
        return false
    }
}

; ============================================
; FUNCTION: Get specific key by index
; ============================================
GetKeyByIndex(keysObject, index) {
    if (index > 0 && index <= keysObject.keys.Length) {
        return keysObject.keys[index]
    }
    return false
}

; ============================================
; MAIN EXECUTION
; ============================================

; Example usage:
keys := FetchKeysFromServer()

if (keys) {
    ; Get first key
    firstKey := GetKeyByIndex(keys, 1)
    MsgBox("First key: " . firstKey)
    
    ; Get random key
    randomIndex := Random(1, keys.keys.Length)
    randomKey := GetKeyByIndex(keys, randomIndex)
    MsgBox("Random key (index " . randomIndex . "): " . randomKey)
    
    ; Use keys for your password/encryption system
    ; keys.keys now contains all 50 keys from the server
}

ExitApp
