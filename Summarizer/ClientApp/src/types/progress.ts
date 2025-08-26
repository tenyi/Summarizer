// 進度追蹤相關的 TypeScript 型別定義

/**
 * 處理進度資料介面
 */
export interface ProcessingProgress {
  batchId: string;
  totalSegments: number;
  currentSegment: number;
  completedSegments: number;
  failedSegments: number;
  currentStage: ProcessingStage;
  overallProgress: number;  // 0-100
  stageProgress: number;    // 當前階段進度 0-100
  elapsedTimeMs: number;    // 已花費時間（毫秒）
  estimatedRemainingTimeMs?: number; // 預估剩餘時間（毫秒）
  averageSegmentTimeMs: number;
  currentSegmentTitle?: string;
  processingSpeed: ProcessingSpeed;
  lastUpdated: string;  // ISO 日期字串
  startTime: string;    // ISO 日期字串
  estimatedCompletionTime?: string; // ISO 日期字串
  successRate: number;  // 成功率百分比
}

/**
 * 分段狀態介面
 */
export interface SegmentStatus {
  index: number;
  title: string;
  status: SegmentProcessingStatus;
  startTime?: string;     // ISO 日期字串
  endTime?: string;       // ISO 日期字串
  processingTimeMs?: number;
  errorMessage?: string;
  retryCount: number;
  contentLength: number;
  contentOffset: number;
  resultLength?: number;
  isCompleted: boolean;
  isProcessing: boolean;
}

/**
 * 處理階段枚舉
 */
export enum ProcessingStage {
  Initializing = 'initializing',      // 初始化
  Segmenting = 'segmenting',          // 分段中
  BatchProcessing = 'batch-processing', // 批次處理中
  Merging = 'merging',                // 合併中
  Finalizing = 'finalizing',          // 完成處理中
  Completed = 'completed',            // 已完成
  Failed = 'failed'                   // 處理失敗
}

/**
 * 分段處理狀態枚舉
 */
export enum SegmentProcessingStatus {
  Pending = 'pending',         // 等待中
  Processing = 'processing',   // 處理中
  Completed = 'completed',     // 已完成
  Failed = 'failed',          // 失敗
  Retrying = 'retrying'       // 重試中
}

/**
 * 處理速度統計
 */
export interface ProcessingSpeed {
  segmentsPerMinute: number;
  charactersPerSecond: number;
  currentThroughput: number;
  averageLatencyMs: number;
  maxLatencyMs: number;
  minLatencyMs: number;
  efficiencyPercentage: number;
  calculationWindowMs: number;
  lastCalculated: string; // ISO 日期字串
}

/**
 * 階段定義
 */
export interface StageDefinition {
  stage: ProcessingStage;
  name: string;
  description: string;
  icon: string;
  estimatedDurationPercentage: number;
  isCriticalPath: boolean;
  order: number;
  canRunInParallel: boolean;
}

/**
 * 進度更新事件資料
 */
export interface ProgressUpdateEvent {
  batchId: string;
  progress: ProcessingProgress;
  updateReason: ProgressUpdateReason;
  timestamp: string;
  relatedSegment?: SegmentStatus;
  context: Record<string, any>;
}

/**
 * 進度更新原因枚舉
 */
export enum ProgressUpdateReason {
  InitializationStarted = 'initialization-started',
  SegmentCompleted = 'segment-completed',
  SegmentStarted = 'segment-started',
  SegmentFailed = 'segment-failed',
  StageChanged = 'stage-changed',
  BatchCompleted = 'batch-completed',
  BatchFailed = 'batch-failed',
  PeriodicUpdate = 'periodic-update',
  TimeEstimationUpdate = 'time-estimation-update',
  RetryStarted = 'retry-started'
}

// ===== 部分結果處理相關類型 =====

/**
 * 部分結果資料介面
 */
export interface PartialResult {
  partialResultId: string;
  batchId: string;
  userId: string;
  completedSegments: SegmentSummaryTask[];
  totalSegments: number;
  completionPercentage: number;
  partialSummary: string;
  quality: PartialResultQuality;
  cancellationTime: string; // ISO 日期字串
  userAccepted: boolean;
  acceptedTime?: string; // ISO 日期字串
  status: PartialResultStatus;
  originalTextSample: string;
  processingTime: string; // TimeSpan 字串，如 "00:05:30"
  userComment: string;
}

/**
 * 分段摘要任務介面
 */
export interface SegmentSummaryTask {
  segmentIndex: number;
  sourceSegment: SegmentResult;
  status: SegmentTaskStatus;
  summaryResult: string;
  retryCount: number;
  startTime?: string; // ISO 日期字串
  completedTime?: string; // ISO 日期字串
  errorMessage: string;
  processingTime?: string; // TimeSpan 字串
  lastRetryTime?: string; // ISO 日期字串
}

/**
 * 分段結果介面
 */
export interface SegmentResult {
  content: string;
  startIndex: number;
  endIndex: number;
  title?: string;
  metadata: Record<string, any>;
}

/**
 * 分段任務狀態枚舉
 */
export enum SegmentTaskStatus {
  Pending = 'Pending',
  Processing = 'Processing',
  Completed = 'Completed',
  Failed = 'Failed',
  Retrying = 'Retrying'
}

/**
 * 部分結果狀態枚舉
 */
export enum PartialResultStatus {
  PendingUserDecision = 'PendingUserDecision',
  Accepted = 'Accepted',
  Rejected = 'Rejected',
  Expired = 'Expired',
  Processing = 'Processing',
  Failed = 'Failed'
}

/**
 * 部分結果品質評估介面
 */
export interface PartialResultQuality {
  completenessScore: number; // 0.0 到 1.0
  hasLogicalFlow: boolean;
  coherenceScore: number; // 0.0 到 1.0
  missingTopics: string[];
  qualityWarnings: string[];
  overallQuality: QualityLevel;
  recommendedAction: RecommendedAction;
  coverage: ContentCoverage;
  qualityExplanation: string;
  assessmentTime: string; // ISO 日期字串
}

/**
 * 品質等級枚舉
 */
export enum QualityLevel {
  Unknown = 'Unknown',
  Excellent = 'Excellent',
  Good = 'Good',
  Acceptable = 'Acceptable',
  Poor = 'Poor',
  Unusable = 'Unusable'
}

/**
 * 推薦動作枚舉
 */
export enum RecommendedAction {
  Recommend = 'Recommend',
  ReviewRequired = 'ReviewRequired',
  Discard = 'Discard',
  ConsiderContinue = 'ConsiderContinue'
}

/**
 * 內容覆蓋率資訊介面
 */
export interface ContentCoverage {
  beginningCoverage: number; // 0.0 到 1.0
  middleCoverage: number; // 0.0 到 1.0
  endCoverage: number; // 0.0 到 1.0
  hasContinuousCoverage: boolean;
  maxContinuousLength: number;
  coverageGaps: number;
}

// ===== 取消操作相關類型 =====

/**
 * 取消請求介面
 */
export interface CancellationRequest {
  batchId: string;
  reason: CancellationReason;
  savePartialResults: boolean;
  requestedBy: string;
  requestedAt: string; // ISO 日期字串
  additionalContext?: Record<string, any>;
}

/**
 * 取消結果介面
 */
export interface CancellationResult {
  isSuccessful: boolean;
  reason: CancellationReason;
  cancelledAt: string; // ISO 日期字串
  partialResults?: PartialResult[];
  completedSegmentCount: number;
  totalSegmentCount: number;
  processingTimeBeforeCancellation: string; // TimeSpan 字串
  errorMessage?: string;
  cleanupCompleted: boolean;
  auditLogEntryId: string;
}

/**
 * 取消原因枚舉
 */
export enum CancellationReason {
  UserRequested = 'UserRequested',
  Timeout = 'Timeout', 
  SystemError = 'SystemError',
  ResourceExhaustion = 'ResourceExhaustion',
  BatchNotFound = 'BatchNotFound',
  AlreadyCompleted = 'AlreadyCompleted',
  InvalidState = 'InvalidState'
}

// ===== 錯誤處理相關類型 =====

/**
 * 處理錯誤介面
 */
export interface ProcessingError {
  errorId: string;
  title: string;
  userFriendlyMessage: string;
  errorMessage: string;
  errorCode: string;
  severity: ErrorSeverity;
  category?: ErrorCategory;
  isRecoverable: boolean;
  suggestedActions: string[];
  errorContext?: Record<string, any>;
  timestamp: string; // ISO 日期字串
  retryCount: number;
}

/**
 * 錯誤嚴重程度枚舉
 */
export enum ErrorSeverity {
  Low = 'Low',
  Medium = 'Medium', 
  High = 'High',
  Critical = 'Critical'
}

/**
 * 錯誤類別枚舉
 */
export enum ErrorCategory {
  Network = 'Network',
  Authentication = 'Authentication',
  Authorization = 'Authorization',
  Validation = 'Validation',
  Processing = 'Processing',
  Resource = 'Resource',
  Configuration = 'Configuration',
  External = 'External',
  System = 'System'
}

// ===== 系統恢復相關類型 =====

/**
 * 系統恢復結果介面
 */
export interface SystemRecoveryResult {
  isSuccessful: boolean;
  recoverySteps: RecoveryStep[];
  totalSteps: number;
  completedSteps: number;
  recoveryTimeMs: number;
  recoveredAt: string; // ISO 日期字串
  errorMessage?: string;
  batchStateCleared: boolean;
  resourcesReleased: boolean;
  uiStateReset: boolean;
  performanceMetrics?: PerformanceMetrics;
}

/**
 * 恢復步驟介面
 */
export interface RecoveryStep {
  stepName: string;
  description: string;
  isCompleted: boolean;
  executionTimeMs?: number;
  errorMessage?: string;
  order: number;
}

/**
 * 恢復原因類型
 */
export type RecoveryReason = 
  | 'UserRequested'
  | 'SystemError'
  | 'ErrorRecovery'
  | 'SystemFailure'
  | 'ResourceExhaustion'
  | 'MaintenanceMode'
  | 'DataCorruption'
  | 'ConfigurationChange';

/**
 * 系統健康檢查結果介面
 */
export interface SystemHealthCheckResult {
  isHealthy: boolean;
  overallStatus: SystemHealthStatus;
  componentChecks: ComponentHealthCheck[];
  issues: string[];
  checkedAt: string; // ISO 日期字串
  totalCheckTimeMs: number;
  performanceMetrics?: PerformanceMetrics;
}

/**
 * 系統健康狀態枚舉
 */
export enum SystemHealthStatus {
  Healthy = 'Healthy',
  Warning = 'Warning',
  Degraded = 'Degraded',
  Critical = 'Critical',
  Unknown = 'Unknown'
}

/**
 * 元件健康檢查介面
 */
export interface ComponentHealthCheck {
  componentName: string;
  isHealthy: boolean;
  status: SystemHealthStatus;
  checkDurationMs: number;
  issues: string[];
  details?: Record<string, any>;
}

/**
 * 自我修復結果介面
 */
export interface SelfRepairResult {
  isSuccessful: boolean;
  repairedIssues: string[];
  unableToRepair: string[];
  repairActions: RepairAction[];
  totalRepairTimeMs: number;
  repairedAt: string; // ISO 日期字串
  needsManualIntervention: boolean;
  recommendations: string[];
}

/**
 * 修復動作介面
 */
export interface RepairAction {
  actionName: string;
  description: string;
  isSuccessful: boolean;
  executionTimeMs: number;
  errorMessage?: string;
  category: RepairCategory;
}

/**
 * 修復類別枚舉
 */
export enum RepairCategory {
  Performance = 'Performance',
  Resource = 'Resource',
  Connection = 'Connection',
  Data = 'Data',
  Configuration = 'Configuration',
  Process = 'Process'
}

/**
 * 恢復狀態介面
 */
export interface RecoveryStatus {
  batchId: string;
  isInProgress: boolean;
  currentStep: string;
  progress: number; // 0-100
  startedAt?: string; // ISO 日期字串
  estimatedCompletionTime?: string; // ISO 日期字串
  lastUpdated: string; // ISO 日期字串
  error?: string;
}

/**
 * 效能指標介面
 */
export interface PerformanceMetrics {
  cpuUsagePercentage: number;
  memoryUsageMB: number;
  diskUsagePercentage: number;
  errorRate: number;
  averageResponseTimeMs: number;
  throughput: number;
  measuredAt: string; // ISO 日期字串
}

/**
 * 預設階段定義
 */
export const DEFAULT_STAGE_DEFINITIONS: StageDefinition[] = [
  {
    stage: ProcessingStage.Initializing,
    name: '初始化',
    description: '準備處理環境和資源',
    icon: 'settings',
    estimatedDurationPercentage: 5,
    order: 1,
    isCriticalPath: true,
    canRunInParallel: false
  },
  {
    stage: ProcessingStage.Segmenting,
    name: '文本分段',
    description: '將長文本分割成處理單元',
    icon: 'cut',
    estimatedDurationPercentage: 10,
    order: 2,
    isCriticalPath: true,
    canRunInParallel: false
  },
  {
    stage: ProcessingStage.BatchProcessing,
    name: '批次處理',
    description: 'AI 模型處理各個分段',
    icon: 'cpu',
    estimatedDurationPercentage: 70,
    order: 3,
    isCriticalPath: true,
    canRunInParallel: true
  },
  {
    stage: ProcessingStage.Merging,
    name: '結果合併',
    description: '整合各分段的處理結果',
    icon: 'merge',
    estimatedDurationPercentage: 10,
    order: 4,
    isCriticalPath: true,
    canRunInParallel: false
  },
  {
    stage: ProcessingStage.Finalizing,
    name: '完成處理',
    description: '最終化結果和清理工作',
    icon: 'check',
    estimatedDurationPercentage: 5,
    order: 5,
    isCriticalPath: true,
    canRunInParallel: false
  }
];