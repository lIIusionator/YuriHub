namespace Yuri.Modules.FastFlags;

/// <summary>FFM_HITBOX and FFM_HITBOX30: the two built-in presets, bare names as the .ahk staged them.</summary>
public static class FfmPresets
{
    static KeyValuePair<string, string> P(string k, string v) => new(k, v);
    public static readonly KeyValuePair<string, string>[] Hitbox =
    {
        P("AirControllerTurningResponsiveness", "2147483647"), P("AnimationStreamSourceUseRuntimeSyncPrims", "True"),
        P("AnimationStreamTrackUseRuntimeSyncPrims", "True"), P("AnimationTrackStepFix", "True"),
        P("CliMaxChRcv", "2147483647"), P("CliMaxChSnd", "2147483647"), P("ClientPacketMaxDelayMs", "1"),
        P("ClusterSenderMaxJoinBandwidthBps", "100000000"), P("ClusterSenderMaxUpdateBandwidthBps", "100000000"),
        P("DataSenderMaxJoinBandwidthBps", "100000000"), P("DebugForceGenerateHSR", "True"),
        P("DebugGraphicsPreferD3D11", "True"), P("DebugSimAdaptiveIncreaseCollisionStiffness", "True"),
        P("DebugSimForceAdaptiveTimeStepping", "True"), P("DisableDPIScale", "True"), P("EnableTexturePreloading", "True"),
        P("GraphicsGLEnableHQShadersExclusion", "True"), P("GraphicsMTLoadShaderGlesPVR", "True"),
        P("GraphicsOptimizationModePerformanceBiasPercent", "100"), P("HumanoidStateUseRuntimeSyncPrims", "True"),
        P("KeyframeSequenceUseRuntimeSyncPrims", "True"), P("LuauSolverV2", "True"),
        P("MaxClientSimulationRadius", "2147000000"), P("MaxDataPayloadSize", "2147483647"),
        P("MaxProcessPacketsJobScaling", "2147483647"), P("MaxProcessPacketsStepsAccumulated", "1"),
        P("MaxProcessPacketsStepsPerCyclic", "1"), P("MinClientSimulationRadius", "2147000000"), P("Network", "7"),
        P("NetworkStopProducingPacketsToProcessThresholdMs", "1"), P("PhysicsNOUCountHundredth", "2147483647"),
        P("PhysicsReceiveNumParallelTasks", "16"), P("PhysicsSenderMaxBandwidthBps", "100000000"),
        P("QuaternionPoseCorrection", "True"), P("S2PhysicsSenderRate", "2147483647"), P("SendGameServerDataMaxLen", "64736"),
        P("SimAdaptiveHumanoidPDControllerSubstepMultiplier", "150"), P("SimDefaultFluidForceEnabled", "12"),
        P("SimDefaultHumanoidTimestepMultiplier", "150"), P("SimTimestepMultiplierDebounceCount", "0"),
        P("SmoothMouseSpringFrequencyTenths", "0"), P("TargetRefreshRate", "100"),
        P("TaskSchedulerAsyncTasksMinimumThreadCount", "2"), P("TaskSchedulerJobInGameExecFreqMax", "2147483647"),
        P("TaskSchedulerJobInGameExecFreqMin", "2147483647"), P("TaskSchedulerMaxNumOfArbiters", "2147483647"),
        P("TaskSchedulerMaxNumOfJobs", "2139999999"), P("TaskSchedulerThreadMin", "3"), P("TextureQualityOverride", "3"),
        P("TextureQualityOverrideEnabled", "True"), P("TouchSenderMaxBandwidthBps", "100000000"),
    };
    public static readonly KeyValuePair<string, string>[] Hitbox30 =
    {
        P("AirControllerTurningResponsiveness", "2147483647"), P("AnimationStreamSourceUseRuntimeSyncPrims", "True"),
        P("AnimationStreamTrackUseRuntimeSyncPrims", "True"), P("AnimationTrackStepFix", "True"),
        P("CliMaxChRcv", "2147483647"), P("CliMaxChSnd", "2147483647"), P("ClientPacketMaxDelayMs", "1"),
        P("ClusterSenderMaxJoinBandwidthBps", "100000000"), P("ClusterSenderMaxUpdateBandwidthBps", "100000000"),
        P("DFFlagReplicateCreateToPlayer", "True"),
        P("DFIntAnimatorRetargetInterpolateFKCorrectionMaxAngleDeg", "2147483647"),
        P("DFIntAnimatorRetargetInterpolateFKCorrectionMinAngleDeg", "2147483647"),
        P("DFIntCheckPVDifferencesForInterpolationMinVelThresholdStudsPerSecHundredth", "2147483647"),
        P("DFIntCloseReplicationDataAliveForAllHundredthsPercentage", "1000000"),
        P("DFIntGameplayNetInterpolationDistanceCorrectionSampleMillionth", "0"),
        P("DFIntInterpolationDtLimitForLod", "0"), P("DFIntInterpolationFramePositionThresholdMillionth", "0"),
        P("DFIntInterpolationFrameRotVelocityThresholdMillionth", "2147483647"),
        P("DFIntInterpolationFrameVelocityThresholdMillionth", "2147483647"), P("DFIntInterpolationMinAssemblyCount", "1"),
        P("DFIntInterpolationNumMechanismsBatchSize", "1"), P("DFIntInterpolationNumMechanismsPerTask", "1"),
        P("DFIntInterpolationNumParallelTasks", "16"), P("DFIntMaxInterpolationRecursionsBeforeCheck", "0"),
        P("DFIntNumFramesToKeepAfterInterpolation", "0"), P("DFIntNumTrackerDataToReplicatePerFrame", "2147483647"),
        P("DFIntReplicationDataCacheNumParallelTasks", "16"), P("DataSenderMaxJoinBandwidthBps", "100000000"),
        P("DebugSimAdaptiveIncreaseCollisionStiffness", "True"), P("DebugSimForceAdaptiveTimeStepping", "True"),
        P("FIntInterpolationAwareTargetTimeLerpHundredth", "115"), P("FIntInterpolationMaxDelayMSec", "0"),
        P("GraphicsGLEnableHQShadersExclusion", "True"), P("GraphicsMTLoadShaderGlesPVR", "True"),
        P("GraphicsOptimizationModePerformanceBiasPercent", "100"), P("HumanoidStateUseRuntimeSyncPrims", "True"),
        P("KeyframeSequenceUseRuntimeSyncPrims", "True"), P("MaxClientSimulationRadius", "2147000000"),
        P("MaxDataPayloadSize", "2147483647"), P("MaxProcessPacketsJobScaling", "2147483647"),
        P("MaxProcessPacketsStepsAccumulated", "1"), P("MaxProcessPacketsStepsPerCyclic", "1"),
        P("MinClientSimulationRadius", "2147000000"), P("NetworkStopProducingPacketsToProcessThresholdMs", "1"),
        P("PhysicsNOUCountHundredth", "2147483647"), P("PhysicsReceiveNumParallelTasks", "16"),
        P("PhysicsSenderMaxBandwidthBps", "100000000"), P("QuaternionPoseCorrection", "True"),
        P("S2PhysicsSenderRate", "2147483647"), P("SendGameServerDataMaxLen", "64736"),
        P("SimAdaptiveHumanoidPDControllerSubstepMultiplier", "150"), P("SimDefaultHumanoidTimestepMultiplier", "150"),
        P("SimTimestepMultiplierDebounceCount", "0"), P("TaskSchedulerJobInGameExecFreqMax", "2147483647"),
        P("TaskSchedulerJobInGameExecFreqMin", "2147483647"), P("TaskSchedulerMaxNumOfArbiters", "2147483647"),
        P("TouchSenderMaxBandwidthBps", "100000000"),
    };
}
