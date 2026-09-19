using BetterGIProWpf.Services; using BetterGIProWpf.Services.Knowledge; namespace BetterGIProWpf;
public static class AppState { public static List<OperationStep> Steps{get;set;}=new(); public static CronScheduler Sched{get;}=new(); public static KnowledgeBase KB{get;}=new(); }
