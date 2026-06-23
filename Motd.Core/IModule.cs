namespace Motd.Core;

internal interface IModule
{
    bool Init();

    void OnPostInit() { }

    void OnAllSharpModulesLoaded() { }

    void Shutdown() { }
}
