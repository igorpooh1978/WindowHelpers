При инициализации плагина
        IikoWindows.Helpers.InitializeUiDispatcher();

При выгрузке плагина
        IikoWindows.Helpers.ShutdownUiDispatcher();

вызов блокирующего окна
 var window = IikoWindows.Helpers.ShowWindow<UserInfoWindow, UserInfoProperties, object>(
     new UserInfoProperties
     {
         AlreadyInOrder = alreadyInOrder,
         Worker = worker,
         Client = result,
         ViewManager = obj.vm,
         OperationService = obj.os,
         Order = obj.order,
         DebugMode = _appSettings?.Value.DebugMode ?? false
     });
