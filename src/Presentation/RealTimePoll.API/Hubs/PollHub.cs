using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RealTimePoll.API.Hubs;

public class PollHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> _pollGroups = new();

    public async Task JoinPoll(string pollId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"poll_{pollId}");

        lock (_pollGroups)
        {
            if (!_pollGroups.ContainsKey(pollId))
                _pollGroups[pollId] = new HashSet<string>();
            _pollGroups[pollId].Add(Context.ConnectionId);
        }

        await Clients.Caller.SendAsync("JoinedPoll", pollId);
    }

    public async Task LeavePoll(string pollId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"poll_{pollId}");

        lock (_pollGroups)
        {
            if (_pollGroups.ContainsKey(pollId))
                _pollGroups[pollId].Remove(Context.ConnectionId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        lock (_pollGroups)
        {
            foreach (var group in _pollGroups.Values)
                group.Remove(Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public int GetViewerCount(string pollId)
    {
        lock (_pollGroups)
        {
            return _pollGroups.TryGetValue(pollId, out var group) ? group.Count : 0;
        }
    }
}
