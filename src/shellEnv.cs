using static c69_shellTools.c69_shellTools;
using System.Collections.Generic;
using System;

namespace c69_shellEnv
{
    enum types {
        nullType,
        stringType,
        intType,
        floatType
    };

    struct envVar
    {
        public string name;
        public string value;
        public int    type;
        public bool   isReadOnly;
    }

    class shellEnv
    {
        public List<string> taskHistory = new List<string>();
        private static Dictionary<string, envVar> env = new Dictionary<string, envVar>();

        public shellEnv(bool hasEnvFile=false)
        {
            if (hasEnvFile)
            {
                // read the env file
                readEnvFile("./env/env.conf");
                return;
            }

            // set the env dictionary
            setEnv("PATH" , "");
            setEnv("HOME" , "");
            setEnv("USER" , "");
            setEnv("PWD"  , "");
            setEnv("ENV_FILE", "./env/env.conf", true);
            setEnv("PROMPT", "C#69> ");
            setEnv("TRUE" , "1"   , true);
            setEnv("FALSE", "0"   , true);
            setEnv("NULL" , "NULL", true);
            setEnv("taskCount", "0");
            setEnv("lastTaskExitCode", "0");
        }

        private int interpretType(string value) {
            // check if the value is a number
            if (value == "NULL") { return (int)types.nullType; }
            bool isFloat = float.TryParse(value, out float f);
            if (isFloat == true) { return (int) types.floatType; }
            bool isInt   = int.TryParse(value, out int i);
            if (isInt == true)   { return (int) types.intType; }
            return (int) types.stringType;
        }

        public bool varExists(string key)
        {
            return env.ContainsKey(key);
        }

        public bool varIsReadOnly(string key)
        {
            if (env.ContainsKey(key))
            {
                return env[key].isReadOnly;
            }
            return false;
        }

        public envVar getVar(string key)
        {
            if (env.ContainsKey(key))
            {
                return env[key];
            }
            return env["NULL"];
        }

        public void removeVar(string key)
        {
            if (env.ContainsKey(key) && !env[key].isReadOnly)
            {
                env.Remove(key);
                return;
            }
            Console.WriteLine("Error: Cannot remove read-only variable (" + key + ")");
        }

        public void listEnv()
        {
            foreach (KeyValuePair<string, envVar> kvp in env)
            {
                Console.WriteLine(kvp.Key + " = " + kvp.Value.value);
            }
        }

        public void readEnvFile(string fileName)
        {
            List<string> lines = readFile(fileName);
            // reset the env dictionary
            env.Clear();
            foreach (string line in lines)
            {
                List<string> tokens = split(line, ';');
                setEnv(tokens[0], tokens[1], bool.TryParse(tokens[2], out bool b) ? b : false);
            }
        }

        public void writeEnvFile(string fileName)
        {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, envVar> kvp in env)
            {
                lines.Add(kvp.Key + ";" + kvp.Value.value + ";" + kvp.Value.isReadOnly);
            }
            writeFile(fileName, lines);
        }

        public void setEnv(string key, string value, bool isReadonly=false)
        {
            if (varIsReadOnly(key))
            {
                Console.WriteLine("Error: variable " + key + " is read only");
                return;
            }

            envVar var = new envVar();
            var.name = key;
            var.value = value;
            var.type = interpretType(value);
            var.isReadOnly = isReadonly;
            if (varExists(key))
                env.Remove(key);
            
            env.Add(key, var);
        }
    }
}