document.getElementById('register').addEventListener('submit', handleRegisterSubmit);

async function handleRegisterSubmit(event) {
    event.preventDefault();

    // possible values: none, direct, indirect
    let attestation_type = "none";
    // possible values: <empty>, platform, cross-platform
    let authenticator_attachment = "";

    // possible values: preferred, required, discouraged
    let user_verification = "preferred";

    // possible values: true,false
    let residentKey = "discouraged";

    // prepare form post data
    var data = new FormData(this);
    data.append('attType', attestation_type);
    data.append('authType', authenticator_attachment);
    data.append('userVerification', user_verification);
    data.append('residentKey', residentKey);

    let challengeKey;
    let publicKeyOptions;
    try {
        ({ options: publicKeyOptions, challengeKey } = await fetchMakeCredentialOptions(data));
    } catch (e) {
        console.error(e);
    }

    console.log("Credential Options Object", publicKeyOptions);

    if (publicKeyOptions.status !== "ok") {
        console.log("Error creating credential options:", publicKeyOptions.errorMessage);
        return;
    }

    // Turn the challenge back into the accepted format of padded base64
    publicKeyOptions.challenge = coerceToArrayBuffer(publicKeyOptions.challenge);
    // Turn ID into a UInt8Array Buffer for some reason
    publicKeyOptions.user.id = coerceToArrayBuffer(publicKeyOptions.user.id);

    publicKeyOptions.excludeCredentials = publicKeyOptions.excludeCredentials.map(c => ({
        ...c,
        id: coerceToArrayBuffer(c.id)
    }));

    if (publicKeyOptions.authenticatorSelection.authenticatorAttachment === null)
        publicKeyOptions.authenticatorSelection.authenticatorAttachment = undefined;

    console.log("Credential Options Formatted", publicKeyOptions);

    console.log("Creating PublicKeyCredential...");
    let newCredential;
    try {
        newCredential = await navigator.credentials.create({ publicKey: publicKeyOptions });
    } catch (e) {
        const msg = "Could not create credentials in browser. Probably because the username is already registered with your authenticator. Please change username or authenticator."
        console.error(msg, e);
    }

    console.log("PublicKeyCredential Created", newCredential);

    try {
        registerNewCredential(newCredential, challengeKey);
    } catch (e) {
        console.error(err.message ? err.message : err);
    }
}

async function fetchMakeCredentialOptions(formData) {
    let response = await fetch('/api/register/credential-options', {
        method: 'POST',
        body: formData,
        headers: { 'Accept': 'application/json' }
    });

    return await response.json();
}


// This should be used to verify the auth data with the server
async function registerNewCredential(newCredential, challengeKey) {
    // Move data into Arrays in case it is super long
    let attestationObject = new Uint8Array(newCredential.response.attestationObject);
    let clientDataJSON = new Uint8Array(newCredential.response.clientDataJSON);
    let rawId = new Uint8Array(newCredential.rawId);

    const data = {
        id: newCredential.id,
        rawId: coerceToBase64Url(rawId),
        type: newCredential.type,
        extensions: newCredential.getClientExtensionResults(),
        response: {
            AttestationObject: coerceToBase64Url(attestationObject),
            clientDataJson: coerceToBase64Url(clientDataJSON)
        }
    };

    let response;
    try {
        response = await registerCredentialWithServer(data, challengeKey);
    } catch (e) {
        console.error(e);
    }

    console.log("Credential Object", response);

    // show error
    if (response.status !== "ok") {
        console.log("Error creating credential", response.errorMessage);
    }
}

async function registerCredentialWithServer(formData, challengeKey) {
    let response = await fetch('/api/register/credential', {
        method: 'POST',
        body: JSON.stringify(formData),
        headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json',
            'X-Challenge-Key': challengeKey
        }
    });

    return await response.json();
}
