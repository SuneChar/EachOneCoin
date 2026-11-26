using System.Collections;
using Mirror;
using UnityEngine;

namespace EachOneCoin.Networking
{
    public struct LanAuthRequest : NetworkMessage { public bool resume; public string token; }
    public struct VoluntaryLeaveRequest : NetworkMessage { }
    public struct VoluntaryLeaveAcknowledged : NetworkMessage { }
    public struct ResumeComplete : NetworkMessage { }
    public struct ResumeRejected : NetworkMessage { }
    public struct HostClosing : NetworkMessage { }
    public struct LanAuthResponse : NetworkMessage
    {
        public byte code, slot, occupied;
        public bool resumed, fillNpcs;
        public float secondsRemaining;
        public string token;
    }

    public sealed class LanReconnectAuthenticator : NetworkAuthenticator
    {
        private LanNetworkManager manager;
        private void Awake() { manager = GetComponent<LanNetworkManager>(); }

        public override void OnStartServer() => NetworkServer.RegisterHandler<LanAuthRequest>(OnRequest, false);
        public override void OnStopServer() => NetworkServer.UnregisterHandler<LanAuthRequest>();
        public override void OnStartClient() => NetworkClient.RegisterHandler<LanAuthResponse>(OnResponse, false);
        public override void OnStopClient() => NetworkClient.UnregisterHandler<LanAuthResponse>();
        public override void OnServerAuthenticate(NetworkConnectionToClient connection) => StartCoroutine(RejectIfUnverified(connection));
        public override void OnClientAuthenticate() => NetworkClient.Send(new LanAuthRequest { resume = manager.IsReconnectAttempt, token = manager.ReconnectToken ?? string.Empty });

        private void OnRequest(NetworkConnectionToClient connection, LanAuthRequest request)
        {
            if (connection.isAuthenticated) return;
            if (request.token != null && request.token.Length > 128) { ServerReject(connection); return; }
            byte code = manager.AuthenticateConnection(connection, request.resume, request.token, out byte slot, out string token, out bool resumed);
            connection.Send(new LanAuthResponse { code = code, slot = slot, token = token ?? string.Empty,
                resumed = resumed, occupied = manager.HumanSeatMask, fillNpcs = manager.CurrentFillNpcs,
                secondsRemaining = resumed ? manager.ServerRemaining(slot) : 0f });
            if (code == 0) ServerAccept(connection);
            else StartCoroutine(RejectAfterReply(connection));
        }

        private void OnResponse(LanAuthResponse response)
        {
            if (response.code != 0)
            {
                manager.OnAuthenticationRejected(response.code);
                ClientReject();
                return;
            }
            manager.OnAuthenticationAccepted(response.slot, response.token, response.resumed, response.occupied, response.fillNpcs, response.secondsRemaining);
            ClientAccept();
        }

        private IEnumerator RejectIfUnverified(NetworkConnectionToClient connection)
        {
            yield return new WaitForSecondsRealtime(5f);
            if (NetworkServer.active && connection != null && !connection.isAuthenticated &&
                NetworkServer.connections.TryGetValue(connection.connectionId, out var current) && ReferenceEquals(current, connection)) ServerReject(connection);
        }
        private IEnumerator RejectAfterReply(NetworkConnectionToClient connection)
        {
            yield return new WaitForSecondsRealtime(.3f);
            if (NetworkServer.active && connection != null && !connection.isAuthenticated &&
                NetworkServer.connections.TryGetValue(connection.connectionId, out var current) && ReferenceEquals(current, connection)) ServerReject(connection);
        }
    }
}
