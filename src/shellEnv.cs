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

    struct envFunction {
        public string name;
        public int numArgs;
        public List<string> argNames;
        public List<string> code;
    };

    struct envVar
    {
        public string name;
        public string value;
        public int    type;
        public bool   isReadOnly;
    }

    struct envBlock 
    {
        public string name;
        public List<string> code;

    }

    class shellEnv
    {
        public List<string> taskHistory = new List<string>();
        public static Dictionary<string, envBlock> blocks = new Dictionary<string, envBlock>();
        public static Dictionary<string, envVar> env = new Dictionary<string, envVar>();
        public static Dictionary<string, envFunction> functions = new Dictionary<string, envFunction>();
        public static Dictionary<string, string> aliases = new Dictionary<string, string>();
        public shellEnv(bool hasEnvFile=false)
        {
            if (hasEnvFile)
            {
                Console.WriteLine("Loading environment file...");
                // read the env file
                readEnvFile("./env/env.conf");
                return;
            }

            Console.WriteLine("Creating new environment...");

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
            setEnv("LOAD_SETUP_SCRIPT", "1", true);
            setEnv("DEBUG_MODE", "0", false);
            setEnv("lastTaskExitCode", "0");
        }

        public int interpretType(string value) {
            // check if the value is a number
            if (value == "NULL") { return (int)types.nullType; }
            bool isFloat = float.TryParse(value, out float f);
            if (isFloat == true) { return (int) types.floatType; }
            bool isInt   = int.TryParse(value, out int i);
            if (isInt == true)   { return (int) types.intType; }
            return (int) types.stringType;
        }

        public bool blockExists(string key)
        {
            return blocks.ContainsKey(key);
        }

        public envBlock getBlock(string key)
        {
            if (blockExists(key))
                return blocks[key];
            throw new Exception("Block does not exist");
        }

        public void garbageCollect()
        {
            
        }

        public void removeBlock(string key)
        {
            if (blockExists(key))
                blocks.Remove(key);
            else
                throw new Exception("Block does not exist");
        }

        public void setBlock(string key, envBlock block)
        {
            if (blockExists(key))
                blocks[key] = block;
            else
                blocks.Add(key, block);
        }

        public List<string> getBlockNames() {
            List<string> names = new List<string>();
            foreach (string key in blocks.Keys)
                names.Add(key);
            return names;
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

        public void addFunction(string name, envFunction func)
        {
            functions.Add(name, func);
        }

        public List<string> getAliasNames()
        {
            List<string> names = new List<string>();
            foreach (string name in aliases.Keys)
            {
                names.Add(name);
            }
            return names;
        }

        public bool funcExists(string name)
        {
            return functions.ContainsKey(name);
        }

        public envFunction getFunction(string name)
        {
            if (functions.ContainsKey(name))
            {
                return functions[name];
            }
            return functions["NULL"];
        }

        public void loadFunction(List<string> code, string name, List<string> argNames)
        {
            envFunction func = new envFunction();
            func.name = name;
            func.numArgs = argNames.Count;
            func.argNames = argNames;
            func.code = code;
            functions[name] = func;
        }

        public bool aliasExists(string name)
        {
            return aliases.ContainsKey(name);
        }

        public void removeAlias(string name)
        {
            aliases.Remove(name);
        }

        public void addAlias(string name, string value)
        {
            if (aliasExists(name))
            {
                aliases[name] = value;
                return;
            }
            aliases.Add(name, value);
        }

        public string getAlias(string name)
        {
            if (aliasExists(name))
                return aliases[name];
            throw new Exception("Alias does not exist");
        }

        public List<string> getFuncNames() {
            List<string> names = new List<string>();
            foreach (KeyValuePair<string, envFunction> entry in functions)
            {
                names.Add(entry.Key);
            }
            return names;
        }

        public void removeFunction(string name)
        {
            functions.Remove(name);
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

        public envVar makeVar(string name, string value, bool isReadonly = false)
        {
            envVar v = new envVar();
            v.name = name;
            v.value = value;
            v.isReadOnly = false;
            v.type = interpretType(value);
            return v;
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

            envVar var = makeVar(key, value, isReadonly);
            if (varExists(key))
                env.Remove(key);
            
            env.Add(key, var);
        }
    }
}