using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Forms;
using dnlib.DotNet;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.MVVM;
using HoLLy.dnSpyExtension.CodeInjection.Dialogs;
using HoLLy.dnSpyExtension.Common;
using HoLLy.dnSpyExtension.Common.CodeInjection;

namespace HoLLy.dnSpyExtension.CodeInjection.Commands
{
    [ExportMenuItem(OwnerGuid = MenuConstants.APP_MENU_DEBUG_GUID, Group = Constants.AppMenuGroupDebuggerInject, Order = 10)]
    internal class InjectDLL : MenuItemBase
    {
        private DbgManager DbgManager => dbgManagerLazy.Value;
        private DbgProcess CurrentProcess => DbgManager.CurrentProcess.Current
                                             ?? DbgManager.Processes.FirstOrDefault();

        private readonly Lazy<DbgManager> dbgManagerLazy;
        private readonly IManagedInjector injector;
        private readonly Settings settings;

        [ImportingConstructor]
        public InjectDLL(Lazy<DbgManager> dbgManagerLazy, IManagedInjector injector, Settings settings)
        {
            this.dbgManagerLazy = dbgManagerLazy;
            this.injector = injector;
            this.settings = settings;
        }

        public override void Execute(IMenuItemContext context)
        {
            if (!AskForEntryPoint(out InjectionArguments args))
                return;

            settings.AddRecentInjection(args);

            injector.Inject(CurrentProcess.Id, args, CurrentProcess.Bitness == 32, CurrentProcess.Runtimes.First().GetRuntimeType());
        }

        public static bool AskForEntryPoint(out InjectionArguments args)
        {
            args = default;

            var ofd = new OpenFileDialog {
                Filter = PickFilenameConstants.DotNetAssemblyOrModuleFilter,
                Title = "Select .NET assembly to inject",
            };

            if (ofd.ShowDialog() != DialogResult.OK)
                return false;

            AssemblyDef asm;
            try {
                asm = AssemblyDef.Load(ofd.FileName);
            } catch (BadImageFormatException e) {
                MsgBox.Instance.Show("I couldn't load that binary. Are you sure it is a .NET assembly?\n" +
                                     "Exception message: " + e.Message);
                return false;
            }

            var vm = new DLLEntryPointSelectionVM { Assembly = asm, };

            if (!vm.AllItems.Any()) {
                MsgBox.Instance.Show("Couldn't find any suitable entry points in that assembly.\n" +
                                     "Make sure you have a method with the following signature: static int MethodName(string parameter)");
                return false;
            }

            if (new DLLEntryPointSelection(vm).ShowDialog() != true)
                return false;

            args = InjectionArguments.FromMethodDef(vm.SelectedMethod!, vm.Parameter);
            return true;
        }

        public override string GetHeader(IMenuItemContext context)
            => "Inject .NET DLL" + (!injector.IsProcessSupported(CurrentProcess, out string? reason) ? $" ({reason})" : string.Empty);
        public override bool IsVisible(IMenuItemContext context) => DbgManager.IsDebugging;
        public override bool IsEnabled(IMenuItemContext context) => injector.IsProcessSupported(CurrentProcess, out _);
    }
}
