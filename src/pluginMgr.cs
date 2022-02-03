using static c69_shellTools.c69_shellTools;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System;

namespace c69_shellPluginMgr {
    public class pluginMgr {
        // TODO: cache plugin assemblies

        public pluginMgr() {}

        public void loadDll(string path, List<string> args) {
            // get the absolute path
            path = Path.GetFullPath(path);
            var DLL = Assembly.LoadFile(path);

            foreach (Type type in DLL.GetExportedTypes())
            {
                if (type.IsClass && type.IsPublic && type.IsAbstract == false)
                {
                    var plugin = Activator.CreateInstance(type);
                    var pluginFunc = plugin.GetType().GetMethod("run");
                    pluginFunc.Invoke(plugin, new object[] { args.ToArray() });
                } else if (type.IsInterface) {
                    Console.WriteLine("Found interface: " + type.Name);
                } else {
                    Console.WriteLine("Found class: " + type.Name);
                }
            }
        }
    }
}
