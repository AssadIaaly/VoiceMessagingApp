let localConnection;
let remoteConnection;
let localStream;
let remoteStream;
let remoteIceCandidates = [];
let screenStream;
let isVideoStopped = false;
let isAudioMuted = false;

const servers = {
    iceServers: [
        {
            urls: 'turn:82.165.215.103:3478',
            username: 'username',
            credential: 'password'
        }
    ],
    iceTransportPolicy: 'relay'
};

function initializeUserService(dotNetObjectReference) {
    console.log("Initializing user service");
    window.webrtc = {
        startCall: startCall,
        receiveOffer: receiveOffer,
        receiveAnswer: receiveAnswer,
        receiveIceCandidate: receiveIceCandidate,
        endCall: endCall,
        stopVideo: stopVideo,
        muteAudio: muteAudio,
        takeScreenshot: takeScreenshot,
        shareScreen: shareScreen,
        stopScreenShare: stopScreenShare,
        toggleFullScreen: toggleFullScreen,
        dotNetObjectReference: dotNetObjectReference
    };
}

async function startCall(userName, useVideo) {
    console.log(`Starting call to ${userName}`);
    document.getElementById('connectionType').innerText = 'Loading...';
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
    localConnection.oniceconnectionstatechange = () => {
        console.log("ICE connection state change:", localConnection.iceConnectionState);
    };
    localConnection.onicegatheringstatechange = () => {
        console.log("ICE gathering state change:", localConnection.iceGatheringState);
    };
    localConnection.onconnectionstatechange = () => {
        if (localConnection.connectionState === "connected") {
            localConnection.getStats(null).then(stats => {
                stats.forEach(report => {
                    if (report.type === 'candidate-pair' && report.state === 'succeeded') {
                        const localCandidate = stats.get(report.localCandidateId);
                        const remoteCandidate = stats.get(report.remoteCandidateId);
                        const usingTurn = localCandidate.candidateType === 'relay' || remoteCandidate.candidateType === 'relay';
                        document.getElementById('connectionType').innerText = usingTurn ? 'TURN' : 'STUN';
                    }
                });
            });
        }
    };

    const mediaConstraints = { video: useVideo, audio: true };
    try {
        const stream = await navigator.mediaDevices.getUserMedia(mediaConstraints);
        console.log("Media devices accessed:", stream);
        document.getElementById('localVideo').srcObject = stream;
        localStream = stream; // Save the local stream
        stream.getTracks().forEach(track => localConnection.addTrack(track, stream));
        const offer = await localConnection.createOffer();
        console.log("Offer created:", offer);
        await localConnection.setLocalDescription(offer);
        console.log("Local description set:", localConnection.localDescription);
        await webrtc.dotNetObjectReference.invokeMethodAsync('SendOffer', userName, JSON.stringify(localConnection.localDescription));
    } catch (error) {
        console.error('Error starting call', error);
        alert(`Error accessing media devices: ${error.message}`);
    }
}

async function receiveOffer(userName, offer) {
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
    remoteConnection.oniceconnectionstatechange = () => {
        console.log("ICE connection state change:", remoteConnection.iceConnectionState);
    };
    remoteConnection.onicegatheringstatechange = () => {
        console.log("ICE gathering state change:", remoteConnection.iceGatheringState);
    };
    remoteConnection.onconnectionstatechange = () => {
        if (remoteConnection.connectionState === "connected") {
            remoteConnection.getStats(null).then(stats => {
                stats.forEach(report => {
                    if (report.type === 'candidate-pair' && report.state === 'succeeded') {
                        const localCandidate = stats.get(report.localCandidateId);
                        const remoteCandidate = stats.get(report.remoteCandidateId);
                        const usingTurn = localCandidate.candidateType === 'relay' || remoteCandidate.candidateType === 'relay';
                        document.getElementById('connectionType').innerText = usingTurn ? 'TURN' : 'STUN';
                    }
                });
            });
        }
    };

    try {
        const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
        console.log("Media devices accessed:", stream);
        document.getElementById('localVideo').srcObject = stream;
        localStream = stream; // Save the local stream
        stream.getTracks().forEach(track => remoteConnection.addTrack(track, stream));
        await remoteConnection.setRemoteDescription(new RTCSessionDescription(JSON.parse(offer)));
        console.log("Remote description set:", remoteConnection.remoteDescription);
        remoteIceCandidates.forEach(async candidate => {
            await remoteConnection.addIceCandidate(candidate);
            console.log('Queued ICE candidate added successfully');
        });
        remoteIceCandidates = [];
        const answer = await remoteConnection.createAnswer();
        console.log("Answer created:", answer);
        await remoteConnection.setLocalDescription(answer);
        console.log("Local description set:", remoteConnection.localDescription);
        await webrtc.dotNetObjectReference.invokeMethodAsync('SendAnswer', userName, JSON.stringify(remoteConnection.localDescription));
    } catch (error) {
        console.error('Error receiving offer', error);
        alert(`Error accessing media devices: ${error.message}`);
    }
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

    if (screenStream) {
        screenStream.getTracks().forEach(track => track.stop());
        screenStream = null;
    }


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
    document.getElementById('connectionType').innerText = '';
}

function stopVideo() {
    if (localStream) {
        const videoTracks = localStream.getVideoTracks();
        isVideoStopped = !isVideoStopped;
        videoTracks.forEach(track => track.enabled = !isVideoStopped);
        updateButtonState('stopVideoButton', isVideoStopped);
    }
}

function muteAudio() {
    if (localStream) {
        const audioTracks = localStream.getAudioTracks();
        isAudioMuted = !isAudioMuted;
        audioTracks.forEach(track => track.enabled = !isAudioMuted);
        updateButtonState('muteAudioButton', isAudioMuted);
    }
}

function updateButtonState(buttonId, state) {
    const button = document.getElementById(buttonId);
    if (state) {
        button.classList.add('active');
    } else {
        button.classList.remove('active');
    }
}

function takeScreenshot() {
    const video = document.getElementById('remoteVideo');
    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    canvas.getContext('2d').drawImage(video, 0, 0, canvas.width, canvas.height);
    const dataUrl = canvas.toDataURL('image/png');
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = 'screenshot.png';
    a.click();
}
function shareScreen() {
    // if (!localConnection) {
    //     alert('No active connection. Start a call first.');
    //     return;
    // }

    navigator.mediaDevices.getDisplayMedia({ video: true }).then(stream => {
        console.log("Screen sharing started:", stream);
        screenStream = stream;
        const screenTrack = screenStream.getTracks()[0];

        // Replace the video track with the screen sharing track
        const connection = localConnection || remoteConnection;
        
        const sender = connection.getSenders().find(s => s.track.kind === 'video');
        sender.replaceTrack(screenTrack);

        document.getElementById('localVideo').srcObject = screenStream;

        screenTrack.addEventListener('ended', () => {
            console.log("Screen sharing stopped");
            stopScreenShare();
        });
    }).catch(error => {
        console.error("Error sharing screen:", error);
    });
}

function stopScreenShare() {
    if (screenStream) {
        screenStream.getTracks().forEach(track => track.stop());
        screenStream = null;

        const videoTrack = localStream.getVideoTracks()[0];
        const connection = localConnection || remoteConnection;
        const sender = connection.getSenders().find(s => s.track.kind === 'video');
        sender.replaceTrack(videoTrack);

        document.getElementById('localVideo').srcObject = localStream;
    }
}

function toggleFullScreen(elementId) {
    const elem = document.getElementById(elementId);
    if (!document.fullscreenElement) {
        elem.requestFullscreen().catch(err => {
            console.log(`Error attempting to enable full-screen mode: ${err.message} (${err.name})`);
        });
    } else {
        document.exitFullscreen();
    }
}