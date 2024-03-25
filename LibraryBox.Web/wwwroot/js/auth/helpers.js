coerceToArrayBuffer = function (arg, name) {
    if (typeof arg === "string") {
        // base64url to base64
        arg = arg.replace(/-/g, "+").replace(/_/g, "/");

        // base64 to Uint8Array
        const str = window.atob(arg);
        const bytes = new Uint8Array(str.length);
        for (let i = 0; i < str.length; i++)
            bytes[i] = str.charCodeAt(i);
        arg = bytes;
    }

    if (Array.isArray(arg))
        arg = new Uint8Array(arg);

    if (arg instanceof Uint8Array)
        arg = arg.buffer;

    // error if none of the above worked
    if (!(arg instanceof ArrayBuffer))
        throw new TypeError("could not coerce '" + name + "' to ArrayBuffer");

    return arg;
};


coerceToBase64Url = function (arg) {
    // Array or ArrayBuffer to Uint8Array
    if (Array.isArray(arg))
        arg = Uint8Array.from(arg);

    if (arg instanceof ArrayBuffer)
        arg = new Uint8Array(arg);

    // Uint8Array to base64
    if (arg instanceof Uint8Array) {
        let str = "";
        const len = arg.byteLength;

        for (let i = 0; i < len; i++)
            str += String.fromCharCode(arg[i]);
        arg = btoa(str);
    }

    if (typeof arg !== "string")
        throw new Error("could not coerce to string");

    arg = arg.replace(/\+/g, "-").replace(/\//g, "_").replace(/=*$/g, "");

    return arg;
};



// HELPERS

function detectFIDOSupport() {
    if (typeof window.PublicKeyCredential !== "function") {
        const el = document.getElementById("notSupportedWarning");
        if (el)
            el.style.display = 'block';
    }
}