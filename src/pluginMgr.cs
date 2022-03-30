using static c69_shellTools.c69_shellTools;
using System.Collections.Generic;
using System.Reflection;
using c69_shellEnv;
using System.IO;
using System;

// TODO: Fix delegate type
namespace c69_shellPluginMgr {
    public class pluginMgr {

        private Dictionary<string, object> pluginClasses = new Dictionary<string, object>();

        public pluginMgr() {}

        public void callFunction(string assemblyName, string functionName, shellEnv env, params string[] args) {
            if (pluginClasses.ContainsKey(assemblyName)) {
                object pluginClass = pluginClasses[assemblyName];
                MethodInfo method = pluginClass.GetType().GetMethod(functionName);
                method.Invoke(pluginClass, new object[] { env, args });
            } else {
                Console.WriteLine("Error: Assembly not found");
            }
        }

        public void loadDll(string path) {
            path = Path.GetFullPath(path);
            var DLL = Assembly.LoadFile(path);
            string assemblyName = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine("Loading plugin: " + assemblyName);
            foreach(Type type in DLL.GetTypes()) {
                if (type.IsClass) {
                    Console.WriteLine("Found class: " + type.Name);
                    if (type.Name == assemblyName) {
                        Console.WriteLine("Found class: " + type.Name);
                        pluginClasses[assemblyName] = Activator.CreateInstance(type);
                        Console.WriteLine("Loaded plugin: " + assemblyName);
                    }
                }
            }
        }
    }
}
