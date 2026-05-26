export const ProductHandlerSet = {
  none: 1,
  counterChecksum: 2,
  counterChecksumHeavy: 3,
  snapshotCounting: 4,
  unsupported: 5,
} as const;

export const ProductControlAction = {
  stopAccepting: 1,
  drainAccepted: 2,
  cancelOpenAndRelease: 3,
  rejectUnsafeFallback: 4,
} as const;

export type ProductHandlerSetValue =
  (typeof ProductHandlerSet)[keyof typeof ProductHandlerSet];

export type ProductControlActionValue =
  (typeof ProductControlAction)[keyof typeof ProductControlAction];

export type ProductEnumValue = number | string;

export interface ProductApiResponse<T> {
  readonly statusCode: number;
  readonly isSuccess: boolean;
  readonly body: T | null;
  readonly message: string;
}

export interface ProductPipelineOptions {
  readonly workerCount?: number | null;
  readonly workerQueueCapacity?: number | null;
  readonly providerQueueCapacity?: number | null;
  readonly retainedPayloadBytes?: number | null;
  readonly orderedActiveBatchCapacity?: number | null;
  readonly workloadBatchLimit?: number | null;
  readonly silentBorrowedProviderFallback?: boolean;
}

export interface ProductSyntheticRunRequest {
  readonly runId: string;
  readonly sourceCount?: number;
  readonly batchCount?: number;
  readonly eventsPerBatch?: number;
  readonly partitionCount?: number;
  readonly shardCount?: number;
  readonly handlerSet?: ProductHandlerSetValue;
  readonly options?: ProductPipelineOptions | null;
}

export interface ProductArchiveFileRunRequest {
  readonly runId: string;
  readonly filePath: string;
  readonly parallelism?: number;
  readonly partitionCount?: number;
  readonly shardCount?: number;
  readonly decompressor?: string;
  readonly handlerSet?: ProductHandlerSetValue;
  readonly options?: ProductPipelineOptions | null;
}

export interface ProductControlRequest {
  readonly runId: string;
  readonly action: ProductControlActionValue;
  readonly durableStorePath: string;
  readonly sourceCount?: number;
  readonly partitionCount?: number;
  readonly shardCount?: number;
  readonly handlerSet?: ProductHandlerSetValue;
  readonly options?: ProductPipelineOptions | null;
  readonly message?: string;
}

export interface ProductInputSummary {
  readonly kind: ProductEnumValue;
  readonly description: string;
  readonly source: string;
  readonly batchCount: number;
  readonly eventCount: number;
}

export interface ProductConfigurationValue {
  readonly name: string;
  readonly value: string;
  readonly source: ProductEnumValue;
}

export interface ProductConfiguration {
  readonly profileName: string;
  readonly isValid: boolean;
  readonly firstInvalidOption: string | null;
  readonly firstInvalidReason: string | null;
  readonly values: readonly ProductConfigurationValue[];
  readonly warnings: readonly string[];
}

export interface ProductOperatorSummary {
  readonly runState: ProductEnumValue;
  readonly isReady: boolean;
  readonly processingComplete: boolean;
  readonly handlerMode: ProductEnumValue;
  readonly hasHandlerConflict: boolean;
  readonly handlerBlockingReason: string;
  readonly firstBlockingReason: string;
  readonly fallbackRecommendation: ProductEnumValue;
  readonly firstBlockingBatchId: string | null;
  readonly firstBlockingSequence: number | null;
  readonly firstBlockingState: string | null;
  readonly currentRetainedBatchCount: number;
  readonly currentRetainedPayloadBytes: number;
  readonly releaseHealthy: boolean;
  readonly warnings: readonly string[];
}

export interface ProductCapacityEvidence {
  readonly runId: string;
  readonly profileName: string;
  readonly elapsedMilliseconds: number;
  readonly measuredAllocatedBytes: number;
  readonly acceptedBatchCount: number;
  readonly processedBatchCount: number;
  readonly committedBatchCount: number;
  readonly handlerMode: ProductEnumValue;
  readonly durableAdapterKind: string;
  readonly terminalRetainedBatchCount: number;
  readonly terminalRetainedPayloadBytes: number;
  readonly processingCompletenessPassed: boolean;
  readonly isReady: boolean;
  readonly firstBlockingReason: string;
  readonly configurationContour: string;
}

export interface ProductDiagnostics {
  readonly processingCompletenessPassed: boolean;
  readonly isReady: boolean;
  readonly blockingReason: string;
  readonly handlerOutputProvenance: string;
  readonly usesOrderedHandlerDeltaMerge: boolean;
  readonly usesSequentialHandlerFallback: boolean;
  readonly handlerOutputBlocked: boolean;
  readonly releaseFailureCount: number;
  readonly terminalRetainedEnvelopeCount: number;
  readonly terminalRetainedPayloadBytes: number;
  readonly currentRetainedBatchCount: number;
  readonly currentRetainedPayloadBytes: number;
  readonly warnings: readonly string[];
}

export interface ProductBatch {
  readonly providerSequence: number;
  readonly wasAccepted: boolean;
  readonly streamEventCount: number;
  readonly payloadBytes: number;
  readonly payloadValueCount: number;
  readonly rawValueChecksum: number;
  readonly processingStatus: string | null;
  readonly isSuccessful: boolean;
  readonly message: string;
  readonly topologyVersion: number | null;
}

export interface ProductSourceIdentity {
  readonly sourceId: number;
  readonly radarOrdinal: number;
  readonly elevationSlot: number;
  readonly azimuthBucket: number;
  readonly rangeBand: number;
}

export interface ProductHandlerOutput {
  readonly handlerIndex: number;
  readonly handlerName: string;
  readonly name: string;
  readonly type: string;
  readonly int64Value: number;
  readonly doubleValue: number;
}

export interface ProductHandlerField {
  readonly handlerIndex: number;
  readonly handlerName: string;
  readonly name: string;
  readonly type: string;
  readonly slotIndex: number;
}

export interface ProductHandlerDescriptor {
  readonly handlerIndex: number;
  readonly name: string;
  readonly int64SlotCount: number;
  readonly doubleSlotCount: number;
  readonly executionClassification: string;
  readonly fields: readonly ProductHandlerField[];
}

export interface ProductHandlerContract {
  readonly statePosture: string;
  readonly message: string;
  readonly firstBlockingReason: string | null;
  readonly isBlocked: boolean;
  readonly handlers: readonly ProductHandlerDescriptor[];
}

export interface ProductSource {
  readonly identity: ProductSourceIdentity;
  readonly isActive: boolean;
  readonly processedEventCount: number;
  readonly processedPayloadValueCount: number;
  readonly rawValueChecksum: number;
  readonly lastMessageTimestampUtcTicks: number;
  readonly processingChecksum: number;
  readonly handlerValues: readonly ProductHandlerOutput[];
}

export interface ProductRunSummary {
  readonly runId: string;
  readonly input: ProductInputSummary;
  readonly state: ProductEnumValue;
  readonly isReady: boolean;
  readonly hasReadModel: boolean;
  readonly handlerMode: ProductEnumValue;
  readonly firstBlockingReason: string;
  readonly fallbackRecommendation: ProductEnumValue;
  readonly batchCount: number;
  readonly sourceCount: number;
  readonly acceptedBatchCount: number;
  readonly processedBatchCount: number;
  readonly committedBatchCount: number;
  readonly warningCount: number;
}

export interface ProductRunDetail {
  readonly summary: ProductRunSummary;
  readonly configuration: ProductConfiguration;
  readonly operatorSummary: ProductOperatorSummary;
  readonly capacityEvidence: ProductCapacityEvidence;
  readonly diagnostics: ProductDiagnostics | null;
  readonly handlerContract: ProductHandlerContract | null;
  readonly batches: readonly ProductBatch[];
  readonly sources: readonly ProductSource[];
  readonly message: string;
  readonly runId: string;
  readonly isReady: boolean;
  readonly hasReadModel: boolean;
}

export interface ProductRunHistoryReadiness {
  readonly storageKind: ProductEnumValue;
  readonly isReady: boolean;
  readonly storageIdentity: string;
  readonly schemaVersion: number;
  readonly loadedRunCount: number;
  readonly rejectedRunCount: number;
  readonly firstBlockingReason: string;
  readonly warnings: readonly string[];
}

export interface ProductControlSummary {
  readonly runId: string;
  readonly action: string;
  readonly operatorSummary: ProductOperatorSummary;
  readonly canceledOpenCount: number;
  readonly releasedCanceledCount: number;
  readonly drainedProcessingCount: number;
  readonly message: string;
}
