using System;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// List of data structure used by the ApiClient requests and responses
    /// </summary>

    //--------- Requests -----------

    [Serializable]
    public struct LoginRequest
    {
        public string email;
        public string username;
        public string password;
    }

    [Serializable]
    public struct AutoLoginRequest
    {
        public string refresh_token;
    }

    [Serializable]
    public struct RegisterRequest
    {
        public string email;
        public string username;
        public string password;
        public string avatar;
    }

    [Serializable]
    public struct EditUserRequest
    {
        public string avatar;
        public string cardback;
    }

    [Serializable]
    public struct EditEmailRequest
    {
        public string email;
    }

    [Serializable]
    public struct EditPasswordRequest
    {
        public string password_previous;
        public string password_new;
    }

    [Serializable]
    public struct FriendAddRequest
    {
        public string username;
    }

    [Serializable]
    public struct AddMatchRequest
    {
        public string tid;
        public string[] players;
        public string mode;
        public bool ranked;
    }

    [Serializable]
    public struct CompleteMatchRequest
    {
        public string tid;
        public string winner;
        public bool canceled; //Ended by the server (maintenance): no winner and no rewards
    }

    [Serializable]
    public struct RewardGainRequest
    {
        public string reward;
    }

    [Serializable]
    public struct BuyPackRequest
    {
        public string pack;
        public int quantity;
    }

    [Serializable]
    public struct BuyCardRequest
    {
        public string card;
        public string variant;
        public int quantity;
    }

    [Serializable]
    public struct OpenPackRequest
    {
        public string pack;
    }

    //--------- Response -----------

    //Maintenance schedule sent by the API with the version, login and heartbeat responses
    [Serializable]
    public struct MaintenanceInfo
    {
        public string state;            //none, scheduled, active
        public string shutdown_at;      //ISO date, display only, clients count down from remain_sec
        public float remain_sec;        //Seconds left before shutdown when scheduled
        public string end_at;           //Expected reopening ISO date, can be empty
        public string message;
        public bool matchmaking_blocked;
        public int block_match_minutes; //Matchmaking stops this many minutes before the shutdown

        public const string StateNone = "none";
        public const string StateScheduled = "scheduled";
        public const string StateActive = "active";

        public bool IsScheduled { get { return state == StateScheduled; } }
        public bool IsActive { get { return state == StateActive; } }
    }

    //Admin message displayed by every client at the same time, listed until its show time
    [Serializable]
    public struct LiveOpsMessage
    {
        public string id;
        public string text;
        public float remain_sec; //Seconds left before display
    }

    //Live operation state, each response holds the full current lists
    [Serializable]
    public struct LiveOpsInfo
    {
        public MaintenanceInfo maintenance;
        public LiveOpsMessage[] messages;
    }

    [Serializable]
    public struct VersionResponse
    {
        public string version;
        public LiveOpsInfo ops;
    }

    [Serializable]
    public struct KeepOnlineResponse
    {
        public string id;
        public string username;
        public LiveOpsInfo ops;
    }

    //503 answer of every API request during maintenance
    [Serializable]
    public struct MaintenanceErrorResponse
    {
        public string error;
        public LiveOpsInfo ops;
    }

    public enum VersionCheckStatus
    {
        Match = 0,          //Client and server versions are the same
        Mismatch = 10,      //Server answered with a different version
        NetworkError = 20,  //Server could not be reached or answered invalid data
        Skipped = 30,       //No API server to compare with (test mode)
        Maintenance = 40,   //Server is in maintenance
    }

    public struct VersionCheckResult
    {
        public VersionCheckStatus status;
        public string client_version;
        public string server_version;
        public string error;
        public LiveOpsInfo ops;

        public bool Passed { get { return status == VersionCheckStatus.Match || status == VersionCheckStatus.Skipped; } }
    }

    [Serializable]
    public struct RegisterResponse
    {
        public string id;
        public string username;
        public string version;
        public bool success;
        public string error;
    }

    [Serializable]
    public struct LoginResponse
    {
        public string id;
        public string username;
        public string refresh_token;
        public string access_token;
        public int permission_level;
        public int validation_level;
        public int duration;
        public string version;
        public LiveOpsInfo ops;
        public string error;
        public bool success;
    }

    [Serializable]
    public struct UserIdResponse
    {
        public string id;
        public string username;
        public string error;
    }

    [Serializable]
    public struct MatchResponse
    {
        public string tid;
        public string[] players;
        public DateTime start;
        public DateTime end;
        public string winner;
        public bool completed;
        public MatchDataResponse[] udata;
    }

    [Serializable]
    public struct MatchDataResponse
    {
        public string username;
        public int rank;
        public DeckData deck;
        public RewardResponse reward;
    }

    [Serializable]
    public struct RewardResponse
    {
        public string tid;
        public int coins;
        public int elo;
        public int xp;
        public string[] cards;
        public string[] decks;
    }

    [Serializable]
    public struct MarketResponse
    {
        public string seller;
        public string card;
        public int price;
        public int quantity;
    }

    [Serializable]
    public struct FriendResponse
    {
        public string username;
        public string server_time;
        public FriendData[] friends;
        public FriendData[] friends_requests;
    }

    [System.Serializable]
    public struct FriendData
    {
        public string username;
        public string avatar;
        public string last_online_time;
    }

}
