let localConnection;
let remoteConnection;
let localStream;
let remoteStream;
let remoteIceCandidates = [];
const servers = {
    iceServers: [
        { urls: 'stun:stun.l.google.com:19302' },
        { urls: 'turn:relay.backups.cz', credential: 'webrtc', username: 'webrtc' },
        { urls: 'turn:relay.metered.ca', credential: 'webrtc', username: 'webrtc' }
    ]
};

function initializeUserService(dotNetObjectReference) {
    console.log("Initializing user service");
    window.webrtc = {
        startCall: startCall,
        receiveOffer: receiveOffer,
        receiveAnswer: receiveAnswer,
        receiveIceCandidate: receiveIceCandidate,
        endCall: endCall,
        dotNetObjectReference: dotNetObjectReference
    };
}

function startCall(userName) {
    console.log(`Starting call to ${userName}`);
    localConnection = new RTCPeerConnection(servers);
    localConnection.onicecandidate = ({ candidate }) => {
        if (candidate) {
            console.log("Local ICE candidate:", candidate);
            sendIceCandidate(userName, candidate);
        }
    };
    localConnection.ontrack = (event) => {
        console.log("Local track received:", event.streams[0]);
        remoteStream = event.streams[0];
        document.getElementById('remoteVideo').srcObject = remoteStream;
    };

    navigator.mediaDevices.getUserMedia({ video: true, audio: true })
        .then(stream => {
            console.log("Media devices accessed:", stream);
            document.getElementById('localVideo').srcObject = stream;
            localStream = stream; // Save the local stream
            stream.getTracks().forEach(track => localConnection.addTrack(track, stream));
            return localConnection.createOffer();
        })
        .then(offer => {
            console.log("Offer created:", offer);
            return localConnection.setLocalDescription(offer);
        })
        .then(() => {
            console.log("Local description set:", localConnection.localDescription);
            webrtc.dotNetObjectReference.invokeMethodAsync('SendOffer', userName, JSON.stringify(localConnection.localDescription));
        })
        .catch(error => console.error('Error starting call', error));
}

function receiveOffer(userName, offer) {
    console.log(`Receiving offer from ${userName}:`, offer);
    remoteConnection = new RTCPeerConnection(servers);
    remoteConnection.onicecandidate = ({ candidate }) => {
        if (candidate) {
            console.log("Remote ICE candidate:", candidate);
            sendIceCandidate(userName, candidate);
        }
    };
    remoteConnection.ontrack = (event) => {
        console.log("Remote track received:", event.streams[0]);
        remoteStream = event.streams[0];
        document.getElementById('remoteVideo').srcObject = remoteStream;
    };

    navigator.mediaDevices.getUserMedia({ video: true, audio: true })
        .then(stream => {
            console.log("Media devices accessed:", stream);
            document.getElementById('localVideo').srcObject = stream;
            localStream = stream; // Save the local stream
            stream.getTracks().forEach(track => remoteConnection.addTrack(track, stream));
            return remoteConnection.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));
        })
        .then(() => {
            console.log("Remote description set:", remoteConnection.remoteDescription);
            remoteIceCandidates.forEach(candidate => {
                remoteConnection.addIceCandidate(candidate)
                    .then(() => console.log('Queued ICE candidate added successfully'))
                    .catch(error => console.error('Error adding queued ICE candidate', error));
            });
            remoteIceCandidates = [];
            return remoteConnection.createAnswer();
        })
        .then(answer => {
            console.log("Answer created:", answer);
            return remoteConnection.setLocalDescription(answer);
        })
        .then(() => {
            console.log("Local description set:", remoteConnection.localDescription);
            webrtc.dotNetObjectReference.invokeMethodAsync('SendAnswer', userName, JSON.stringify(remoteConnection.localDescription));
        })
        .catch(error => console.error('Error receiving offer', error));
}

function receiveAnswer(userName, answer) {
    console.log(`Receiving answer from ${userName}:`, answer);
    localConnection.setRemoteDescription(new RTCSessionDescription(JSON.parse(answer)))
        .then(() => {
            console.log("Remote description set for local connection");
        })
        .catch(error => console.error('Error setting remote description', error));
}

function receiveIceCandidate(userName, candidate) {
    console.log(`Receiving ICE candidate from ${userName}:`, candidate);
    const newCandidate = new RTCIceCandidate(JSON.parse(candidate));
    const connection = localConnection || remoteConnection;
    if (connection.remoteDescription) {
        connection.addIceCandidate(newCandidate)
            .then(() => console.log('ICE candidate added successfully'))
            .catch(error => console.error('Error adding ICE candidate', error));
    } else {
        console.log('Remote description not set yet, queuing ICE candidate');
        remoteIceCandidates.push(newCandidate);
    }
}

async function sendIceCandidate(userName, candidate) {
    console.log(`Sending ICE candidate to ${userName}:`, candidate);
    await webrtc.dotNetObjectReference.invokeMethodAsync('SendIceCandidate', userName, JSON.stringify(candidate));
}

function endCall() {
    console.log("Ending call");

    if (localStream) {
        localStream.getTracks().forEach(track => track.stop());
        localStream = null;
    }

    if (remoteStream) {
        remoteStream.getTracks().forEach(track => track.stop());
        remoteStream = null;
    }

    if (localConnection) {
        localConnection.close();
        localConnection = null;
    }
    if (remoteConnection) {
        remoteConnection.close();
        remoteConnection = null;
    }

    document.getElementById('localVideo').srcObject = null;
    document.getElementById('remoteVideo').srcObject = null;
}
