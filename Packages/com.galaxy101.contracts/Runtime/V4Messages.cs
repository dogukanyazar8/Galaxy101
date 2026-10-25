using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    /// <summary>
    /// v4 protocol (design package <c>protocol/protocol.json</c> 0.4.0) on the existing transport (docs/v4/KARARLAR.md K-04):
    /// one WebSocket, frames <c>{ "type", "id", "data" }</c>. Package <c>t</c> → <c>type</c>, <c>rid</c> → <c>id</c>, fields →
    /// <c>data</c>, the package <c>ack</c> → <see cref="MessageTypes.Ack"/> (<c>data: { id, ok, code, detail, at, ...returns }</c>,
    /// <c>code</c> = the package <c>error</c>), every server event carries <c>data.at</c> (server time, Unix ms).
    /// Account, economy and social messages map to the HTTP endpoints listed in docs/v4/PROTOKOL_ESLEME.md.
    /// </summary>
    public static class V4Messages
    {
        // Client → server (protocol.json c2s, 53)
        public const string Hello = "hello";
        public const string Register = "register";
        public const string Login = "login";
        public const string Guest = "guest";
        public const string PasswordResetRequest = "passwordResetRequest";
        public const string PasswordReset = "passwordReset";
        public const string Oauth = "oauth";
        public const string Logout = "logout";
        public const string AccountDelete = "accountDelete";
        public const string Resync = "resync";
        public const string Ping = "ping";
        public const string ProfileUpdate = "profileUpdate";
        public const string ShopBuy = "shopBuy";
        public const string SkinSet = "skinSet";
        public const string DailyGet = "dailyGet";
        public const string DailyClaim = "dailyClaim";
        public const string QuestClaim = "questClaim";
        public const string FriendsGet = "friendsGet";
        public const string FriendFind = "friendFind";
        public const string FriendRequest = "friendRequest";
        public const string FriendRespond = "friendRespond";
        public const string FriendRemove = "friendRemove";
        public const string LeaderboardGet = "leaderboardGet";
        public const string LobbySubscribe = "lobbySubscribe";
        public const string LobbyUnsubscribe = "lobbyUnsubscribe";
        public const string TableCreate = "tableCreate";
        public const string TableJoin = "tableJoin";
        public const string TableJoinCode = "tableJoinCode";
        public const string TableSpectate = "tableSpectate";
        public const string TableLeave = "tableLeave";
        public const string SeatAddBot = "seatAddBot";
        public const string SeatKick = "seatKick";
        public const string TableStart = "tableStart";
        public const string InviteSend = "inviteSend";
        public const string InviteRespond = "inviteRespond";
        public const string InviteCancel = "inviteCancel";
        public const string VsBotsStart = "vsBotsStart";
        public const string RankedQueue = "rankedQueue";
        public const string RankedAccept = "rankedAccept";
        public const string RankedCancel = "rankedCancel";
        public const string GRollDice = "gRollDice";
        public const string GDraw = "gDraw";
        public const string GReturnSide = "gReturnSide";
        public const string GOpen = "gOpen";
        public const string GLay = "gLay";
        public const string GAdd = "gAdd";
        public const string GSwap = "gSwap";
        public const string GUndo = "gUndo";
        public const string GDiscard = "gDiscard";
        public const string GReady = "gReady";
        public const string GRackOrder = "gRackOrder";
        public const string ChatSend = "chatSend";
        public const string GiftSend = "giftSend";

        // Server → client (protocol.json s2c, 45)
        public const string AckEvent = "ack";
        public const string Session = "session";
        public const string Snapshot = "snapshot";
        public const string Profile = "profile";
        public const string Balance = "balance";
        public const string Xp = "xp";
        public const string Daily = "daily";
        public const string Presence = "presence";
        public const string Friends = "friends";
        public const string FriendRequestIn = "friendRequestIn";
        public const string FriendRequestResult = "friendRequestResult";
        public const string FriendRemoved = "friendRemoved";
        public const string Leaderboard = "leaderboard";
        public const string Lobby = "lobby";
        public const string LobbyTable = "lobbyTable";
        public const string LobbyTableRemoved = "lobbyTableRemoved";
        public const string TableState = "tableState";
        public const string TableClosed = "tableClosed";
        public const string Kicked = "kicked";
        public const string Countdown = "countdown";
        public const string SeatConnection = "seatConnection";
        public const string LeftTable = "leftTable";
        public const string InviteIn = "inviteIn";
        public const string InviteExpired = "inviteExpired";
        public const string InviteResult = "inviteResult";
        public const string RankedSearch = "rankedSearch";
        public const string RankedFound = "rankedFound";
        public const string RankedAccepted = "rankedAccepted";
        public const string RankedCancelled = "rankedCancelled";
        public const string RankedReady = "rankedReady";
        public const string RankChange = "rankChange";
        public const string MatchStart = "matchStart";
        public const string DealStart = "dealStart";
        public const string DealStep = "dealStep";
        public const string DealDie = "dealDie";
        public const string HandDealt = "handDealt";
        public const string Turn = "turn";
        public const string Legal = "legal";
        public const string Game = "game";
        public const string HandWait = "handWait";
        public const string ReadyState = "readyState";
        public const string MatchResult = "matchResult";
        public const string Chat = "chat";
        public const string Gift = "gift";
        public const string TableView = "tableView";
    }

    /// <summary>Every error code of the package (protocol.json errors, 94). The ack's <c>code</c> and HTTP <c>error</c> carry these.</summary>
    public static class V4Errors
    {
        public const string E_BAD_MESSAGE = "E_BAD_MESSAGE";
        public const string E_UNKNOWN_MESSAGE = "E_UNKNOWN_MESSAGE";
        public const string E_AUTH_REQUIRED = "E_AUTH_REQUIRED";
        public const string E_SESSION_EXPIRED = "E_SESSION_EXPIRED";
        public const string E_EMAIL_INVALID = "E_EMAIL_INVALID";
        public const string E_PASS_SHORT = "E_PASS_SHORT";
        public const string E_NAME_INVALID = "E_NAME_INVALID";
        public const string E_EMAIL_EXISTS = "E_EMAIL_EXISTS";
        public const string E_NAME_TAKEN = "E_NAME_TAKEN";
        public const string E_ACCOUNT_UNKNOWN = "E_ACCOUNT_UNKNOWN";
        public const string E_PASS_WRONG = "E_PASS_WRONG";
        public const string E_ACCOUNT_LOCKED = "E_ACCOUNT_LOCKED";
        public const string E_CONFIRM_MISMATCH = "E_CONFIRM_MISMATCH";
        public const string E_IN_GAME = "E_IN_GAME";
        public const string E_STATUS_LONG = "E_STATUS_LONG";
        public const string E_FRAME_LOCKED = "E_FRAME_LOCKED";
        public const string E_SKU_UNKNOWN = "E_SKU_UNKNOWN";
        public const string E_ALREADY_OWNED = "E_ALREADY_OWNED";
        public const string E_NO_GEMS = "E_NO_GEMS";
        public const string E_NOT_OWNED = "E_NOT_OWNED";
        public const string E_DAILY_CLAIMED = "E_DAILY_CLAIMED";
        public const string E_QUEST_UNKNOWN = "E_QUEST_UNKNOWN";
        public const string E_QUEST_INCOMPLETE = "E_QUEST_INCOMPLETE";
        public const string E_QUEST_CLAIMED = "E_QUEST_CLAIMED";
        public const string E_USER_UNKNOWN = "E_USER_UNKNOWN";
        public const string E_ALREADY_FRIENDS = "E_ALREADY_FRIENDS";
        public const string E_REQUEST_PENDING = "E_REQUEST_PENDING";
        public const string E_REQUEST_UNKNOWN = "E_REQUEST_UNKNOWN";
        public const string E_NOT_FRIENDS = "E_NOT_FRIENDS";
        public const string E_ALREADY_SEATED = "E_ALREADY_SEATED";
        public const string E_TABLE_NAME = "E_TABLE_NAME";
        public const string E_PIN_FORMAT = "E_PIN_FORMAT";
        public const string E_TABLE_OPTIONS = "E_TABLE_OPTIONS";
        public const string E_NO_CHIPS = "E_NO_CHIPS";
        public const string E_TABLE_CLOSED = "E_TABLE_CLOSED";
        public const string E_PIN_WRONG = "E_PIN_WRONG";
        public const string E_TABLE_FULL = "E_TABLE_FULL";
        public const string E_TABLE_PLAYING = "E_TABLE_PLAYING";
        public const string E_CODE_FORMAT = "E_CODE_FORMAT";
        public const string E_CODE_UNKNOWN = "E_CODE_UNKNOWN";
        public const string E_SPECTATE_OFF = "E_SPECTATE_OFF";
        public const string E_NOT_SEATED = "E_NOT_SEATED";
        public const string E_NOT_OWNER = "E_NOT_OWNER";
        public const string E_STATUS = "E_STATUS";
        public const string E_SEAT_TAKEN = "E_SEAT_TAKEN";
        public const string E_SEAT_EMPTY = "E_SEAT_EMPTY";
        public const string E_TABLE_NOT_FULL = "E_TABLE_NOT_FULL";
        public const string E_USER_OFFLINE = "E_USER_OFFLINE";
        public const string E_USER_BUSY = "E_USER_BUSY";
        public const string E_INVITE_EXPIRED = "E_INVITE_EXPIRED";
        public const string E_ALREADY_QUEUED = "E_ALREADY_QUEUED";
        public const string E_MODE = "E_MODE";
        public const string E_PARTY_SIZE = "E_PARTY_SIZE";
        public const string E_PARTY_RANGE = "E_PARTY_RANGE";
        public const string E_MATCH_EXPIRED = "E_MATCH_EXPIRED";
        public const string E_NOT_QUEUED = "E_NOT_QUEUED";
        public const string E_SPECTATOR = "E_SPECTATOR";
        public const string E_DEALING = "E_DEALING";
        public const string E_NOT_DEALER = "E_NOT_DEALER";
        public const string E_RACK_MISMATCH = "E_RACK_MISMATCH";
        public const string E_CHAT_OFF = "E_CHAT_OFF";
        public const string E_CHAT_QUICK_ONLY = "E_CHAT_QUICK_ONLY";
        public const string E_CHAT_LENGTH = "E_CHAT_LENGTH";
        public const string E_CHAT_QUICK_UNKNOWN = "E_CHAT_QUICK_UNKNOWN";
        public const string E_GIFTS_OFF = "E_GIFTS_OFF";
        public const string E_GIFT_UNKNOWN = "E_GIFT_UNKNOWN";
        public const string E_GIFT_TARGET = "E_GIFT_TARGET";
        public const string E_NOT_YOUR_TURN = "E_NOT_YOUR_TURN";
        public const string E_PHASE = "E_PHASE";
        public const string E_HAND_OVER = "E_HAND_OVER";
        public const string E_TILE_NOT_IN_RACK = "E_TILE_NOT_IN_RACK";
        public const string E_SIDE_EMPTY = "E_SIDE_EMPTY";
        public const string E_SIDE_RETURNED = "E_SIDE_RETURNED";
        public const string E_SIDE_DISCARD = "E_SIDE_DISCARD";
        public const string E_SIDE_MUST_OPEN = "E_SIDE_MUST_OPEN";
        public const string E_RETURN_INVALID = "E_RETURN_INVALID";
        public const string E_DECK_EMPTY = "E_DECK_EMPTY";
        public const string E_MELD_INVALID = "E_MELD_INVALID";
        public const string E_MELD_MIX = "E_MELD_MIX";
        public const string E_MELD_NOT_FOUND = "E_MELD_NOT_FOUND";
        public const string E_ALREADY_OPENED = "E_ALREADY_OPENED";
        public const string E_NOT_OPENED = "E_NOT_OPENED";
        public const string E_OPEN_MODE = "E_OPEN_MODE";
        public const string E_LAST_TILE = "E_LAST_TILE";
        public const string E_ISLE_INVALID = "E_ISLE_INVALID";
        public const string E_ISLE_DISABLED = "E_ISLE_DISABLED";
        public const string E_SWAP_INVALID = "E_SWAP_INVALID";
        public const string E_NO_UNDO = "E_NO_UNDO";
        public const string E_LEAVE_INVALID = "E_LEAVE_INVALID";
        public const string E_ACTION = "E_ACTION";
        public const string E_SPEC_DUP = "E_SPEC_DUP";
        public const string E_RESET_CODE = "E_RESET_CODE";
        public const string E_OAUTH_FAILED = "E_OAUTH_FAILED";
        public const string E_SEAT_NOT_BOT = "E_SEAT_NOT_BOT";
    }
}
