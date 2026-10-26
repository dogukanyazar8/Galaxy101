using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Galaxy101
{
    /// <summary>
    /// Everything the Dereceli screen needs from outside, shaped after the package protocol (protocol.json c2s rankedQueue,
    /// rankedAccept, rankedCancel; s2c rankedSearch, rankedFound, rankedAccepted, rankedCancelled, rankedReady, rankChange).
    /// <see cref="RankedOnlineBackend"/> feeds it from the game server, the state router's demo from the prototype's data.
    /// Every call and event runs on the main thread.
    /// </summary>
    public interface IRankedBackend
    {
        /// <summary>Your standing this season (idx, LP, protection, peak, games, 1st places, history).</summary>
        RankedProfile Profile { get; }
        /// <summary>Your id among <see cref="RankedFound.Players"/>.</summary>
        string MeId { get; }
        /// <summary>Friends you can bring to Duo / Squad (Idx = their league step, Online = can be invited).</summary>
        IReadOnlyList<RankedPlayer> Friends { get; }
        /// <summary>Profile, friends or balance changed.</summary>
        event Action Changed;
        /// <summary>The screen opened: fetch the standing and the friends again.</summary>
        void Refresh();
        /// <summary>The search in progress (null when not queued): a screen that opens mid-search picks it up.</summary>
        RankedSearch Queue { get; }
        /// <summary>The match waiting for accepts (null when none): a screen that opens on rankedFound picks it up.</summary>
        RankedFound Pending { get; }

        /// <summary>Ask a friend into the party. Null = sent; the answer comes as <see cref="PartyAnswered"/>.</summary>
        Task<string> InviteAsync(RankedPlayer friend);
        /// <summary>A friend's answer to the party invite (id, joined).</summary>
        event Action<string, bool> PartyAnswered;

        /// <summary>rankedQueue {mode: solo|duo|squad, party}: null when the search started, else an error code.</summary>
        Task<string> QueueAsync(string mode, IReadOnlyList<string> party);
        /// <summary>rankedCancel.</summary>
        Task CancelAsync();
        /// <summary>rankedAccept {matchId, accept}.</summary>
        Task<string> AcceptAsync(string matchId, bool accept);

        /// <summary>rankedSearch: the search state (range, seats filled).</summary>
        event Action<RankedSearch> Searching;
        /// <summary>rankedFound: a match to accept within <see cref="RankedFound.Seconds"/>.</summary>
        event Action<RankedFound> Found;
        /// <summary>rankedAccepted: who has accepted so far (seat order).</summary>
        event Action<IReadOnlyList<bool>> Accepted;
        /// <summary>rankedCancelled {reason: timeout|declined|cancel}.</summary>
        event Action<string> Cancelled;
        /// <summary>rankedReady: everyone accepted, the table is being set up (loadingMs).</summary>
        event Action<int> Ready;
        /// <summary>matchStart: open the table.</summary>
        event Action<RankedStart> Started;
        /// <summary>rankChange: the match's LP result.</summary>
        event Action<RankedResult> RankChanged;
    }
}
