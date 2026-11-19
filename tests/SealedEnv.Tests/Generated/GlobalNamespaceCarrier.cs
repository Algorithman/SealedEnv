// No namespace: exercises the generator's global-namespace emission path
// (generated partial without a namespace declaration).
using SealedEnv;

[SealedEnv(Prefix = "GLOBAL_")]
internal sealed partial record GlobalNamespaceCarrier([EnvKey] int Port);
