document.getElementById('signin').addEventListener('submit', handleSignInSubmit);

async function handleSignInSubmit(event) {
    event.preventDefault();

    const formData = new FormData(this);

    let challengeKey;
    let makeAssertionOptions;
    try {
        const res = await fetch('/api/signin/options', {
            method: 'POST',
            body: formData,
            headers: { 'Accept': 'application/json' }
        });

        ({ options: makeAssertionOptions, challengeKey } = await res.json());
    } catch (e) {
        console.error("Request to server failed:", e);
    }

    console.log("Assertion Options Object", makeAssertionOptions);

    // show options error to user
    if (makeAssertionOptions.status !== "ok") {
        console.log("Error creating assertion options:", makeAssertionOptions.errorMessage);
        console.error(makeAssertionOptions.errorMessage);
        return;
    }

    publicKeyOptions.challenge = coerceToArrayBuffer(makeAssertionOptions.challenge);

    makeAssertionOptions.allowCredentials.forEach(listItem => {
        listItem.id = coerceToArrayBuffer(listItem.id);
    });

    console.log("Assertion options", makeAssertionOptions);

    // ask browser for credentials (browser will ask connected authenticators)
    let credential;
    try {
        credential = await navigator.credentials.get({ publicKey: makeAssertionOptions })
    } catch (err) {
        console.error(err.message ? err.message : err);
    }

    try {
        await verifyAssertionWithServer(credential, challengeKey);
    } catch (e) {
        console.error("Could not verify assertion", e);
    }
}

/**
 * Sends the credential to the the FIDO2 server for assertion
 * @param {any} assertedCredential
 */
async function verifyAssertionWithServer(assertedCredential, challengeKey) {

    // Move data into Arrays incase it is super long
    let authData = new Uint8Array(assertedCredential.response.authenticatorData);
    let clientDataJSON = new Uint8Array(assertedCredential.response.clientDataJSON);
    let rawId = new Uint8Array(assertedCredential.rawId);
    let sig = new Uint8Array(assertedCredential.response.signature);
    const data = {
        id: assertedCredential.id,
        rawId: coerceToBase64Url(rawId),
        type: assertedCredential.type,
        extensions: assertedCredential.getClientExtensionResults(),
        response: {
            authenticatorData: coerceToBase64Url(authData),
            clientDataJson: coerceToBase64Url(clientDataJSON),
            signature: coerceToBase64Url(sig)
        }
    };

    let response;
    try {
        let res = await fetch("/api/signin/assertion", {
            method: 'POST', // or 'PUT'
            body: JSON.stringify(data), // data can be `string` or {object}!
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
                'X-Challenge-Key': challengeKey
            }
        });

        response = await res.json();
    } catch (e) {
        console.error("Request to server failed:", e);
        throw e;
    }

    console.log("Assertion Object", response);

    if (response.status !== "ok") {
        console.log("Error doing assertion:", response.errorMessage);
    }
}
