using Microsoft.AspNetCore.SignalR;
using Summarizer.Models.BatchProcessing;
using System.Collections.Concurrent;

namespace Summarizer.Hubs;

/// <summary>
/// 批次處理 SignalR Hub，用於即時進度更新和連線管理
/// </summary>
public class BatchProcessingHub : Hub
{
    /// <summary>
    /// 連線狀態追蹤字典，記錄每個連線的批次 ID
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> ConnectionBatchMap = new();
    
    /// <summary>
    /// 批次群組成員計數，記錄每個批次群組的連線數
    /// </summary>
    private static readonly ConcurrentDictionary<string, int> BatchGroupCounts = new();

    /// <summary>
    /// 客戶端加入特定的批次處理群組，用於接收進度更新
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    public async Task JoinBatchGroup(string batchId)
    {
        var groupName = $"batch_{batchId}";
        
        // 如果此連線已加入其他批次群組，先離開
        if (ConnectionBatchMap.TryGetValue(Context.ConnectionId, out var existingBatchId))
        {
            await LeaveBatchGroup(existingBatchId);
        }
        
        // 加入新群組
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        ConnectionBatchMap[Context.ConnectionId] = batchId;
        
        // 更新群組成員計數
        BatchGroupCounts.AddOrUpdate(groupName, 1, (key, count) => count + 1);
        
        // 通知客戶端成功加入群組
        await Clients.Caller.SendAsync("JoinedBatchGroup", batchId);
        
        // 向群組其他成員廣播新成員加入（可選）
        await Clients.OthersInGroup(groupName).SendAsync("MemberJoined", Context.ConnectionId);
    }

    /// <summary>
    /// 客戶端離開特定的批次處理群組
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    public async Task LeaveBatchGroup(string batchId)
    {
        var groupName = $"batch_{batchId}";
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        ConnectionBatchMap.TryRemove(Context.ConnectionId, out _);
        
        // 更新群組成員計數
        BatchGroupCounts.AddOrUpdate(groupName, 0, (key, count) => Math.Max(0, count - 1));
        
        // 通知客戶端已離開群組
        await Clients.Caller.SendAsync("LeftBatchGroup", batchId);
        
        // 向群組其他成員廣播成員離開（可選）
        await Clients.OthersInGroup(groupName).SendAsync("MemberLeft", Context.ConnectionId);
    }

    /// <summary>
    /// 客戶端請求當前批次的最新進度資訊
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    public async Task RequestProgressUpdate(string batchId)
    {
        // 這裡可以查詢資料庫或緩存獲取最新進度
        // 目前先發送一個請求確認訊息
        await Clients.Caller.SendAsync("ProgressUpdateRequested", batchId);
    }

    /// <summary>
    /// 客戶端設置進度更新偏好設定
    /// </summary>
    /// <param name="preferences">更新偏好設定</param>
    public async Task SetProgressPreferences(object preferences)
    {
        // 可以存儲客戶端的偏好設定，例如更新頻率等
        await Clients.Caller.SendAsync("PreferencesUpdated", preferences);
    }

    /// <summary>
    /// 客戶端ping，用於保持連線活躍
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }

    /// <summary>
    /// 取得批次群組的成員數量
    /// </summary>
    /// <param name="batchId">批次處理 ID</param>
    public async Task GetGroupMemberCount(string batchId)
    {
        var groupName = $"batch_{batchId}";
        var count = BatchGroupCounts.GetValueOrDefault(groupName, 0);
        await Clients.Caller.SendAsync("GroupMemberCount", batchId, count);
    }

    /// <summary>
    /// 客戶端連接時的處理邏輯
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        // 發送歡迎訊息和連線資訊
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId, DateTime.UtcNow);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客戶端斷線時的處理邏輯
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // 清理連線相關資源
        if (ConnectionBatchMap.TryRemove(Context.ConnectionId, out var batchId))
        {
            var groupName = $"batch_{batchId}";
            
            // 更新群組成員計數
            BatchGroupCounts.AddOrUpdate(groupName, 0, (key, count) => Math.Max(0, count - 1));
            
            // 通知群組其他成員
            await Clients.OthersInGroup(groupName).SendAsync("MemberDisconnected", Context.ConnectionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 伺服器端方法：向特定批次群組推送進度更新
    /// 此方法由後端服務呼叫，不是由客戶端呼叫
    /// </summary>
    /// <param name="batchId">批次 ID</param>
    /// <param name="progress">進度資料</param>
    public async Task NotifyProgressUpdate(string batchId, ProcessingProgress progress)
    {
        var groupName = $"batch_{batchId}";
        await Clients.Group(groupName).SendAsync("ProgressUpdate", progress);
    }

    /// <summary>
    /// 伺服器端方法：向特定批次群組推送分段狀態更新
    /// </summary>
    /// <param name="batchId">批次 ID</param>
    /// <param name="segmentStatus">分段狀態</param>
    public async Task NotifySegmentStatusUpdate(string batchId, SegmentStatus segmentStatus)
    {
        var groupName = $"batch_{batchId}";
        await Clients.Group(groupName).SendAsync("SegmentStatusUpdate", segmentStatus);
    }

    /// <summary>
    /// 伺服器端方法：向特定批次群組推送階段變更
    /// </summary>
    /// <param name="batchId">批次 ID</param>
    /// <param name="newStage">新階段</param>
    /// <param name="stageInfo">階段資訊</param>
    public async Task NotifyStageChanged(string batchId, ProcessingStage newStage, object? stageInfo = null)
    {
        var groupName = $"batch_{batchId}";
        await Clients.Group(groupName).SendAsync("StageChanged", newStage, stageInfo);
    }

    /// <summary>
    /// 伺服器端方法：向特定批次群組推送批次完成
    /// </summary>
    /// <param name="batchId">批次 ID</param>
    /// <param name="result">處理結果</param>
    public async Task NotifyBatchCompleted(string batchId, object result)
    {
        var groupName = $"batch_{batchId}";
        await Clients.Group(groupName).SendAsync("BatchCompleted", batchId, result);
    }

    /// <summary>
    /// 伺服器端方法：向特定批次群組推送批次失敗
    /// </summary>
    /// <param name="batchId">批次 ID</param>
    /// <param name="error">錯誤資訊</param>
    public async Task NotifyBatchFailed(string batchId, string error)
    {
        var groupName = $"batch_{batchId}";
        await Clients.Group(groupName).SendAsync("BatchFailed", batchId, error);
    }

    /// <summary>
    /// 伺服器端方法：廣播系統狀態更新（例如服務重啟、維護等）
    /// </summary>
    /// <param name="statusMessage">狀態訊息</param>
    public async Task BroadcastSystemStatus(string statusMessage)
    {
        await Clients.All.SendAsync("SystemStatusUpdate", statusMessage, DateTime.UtcNow);
    }
}