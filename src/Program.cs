
using static c69_shellTools.c69_shellTools;
using System.Collections.Generic;
using System.Threading;
using c69_shellPluginMgr;
using c69_shellEnv;
using System.IO;
using System;
namespace c69_shell
{
    class Program
    {
        const bool inDev = false;
        static bool isAlive = true;
        
        static shellEnv env = null;
        static List<int> loopStart = new List<int>();
        static List<int> loopEnd = new List<int>();
        static List<int> loopStep = new List<int>();
        static List<int> loopIndex = new List<int>();
        static List<int> loopCurrentIndex = new List<int>();
        static pluginMgr pmgr = new pluginMgr();

        static List<string> currentFile = new List<string>();
        static List<bool> logicChain = new List<bool>() { true };
        static int filePos = 0;

        static void Main(string[] args)
        {
            // get the executable path
            string exePath = ".";
            if (!inDev)
                exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Console.WriteLine(exePath);
            if (env == null)
            {
                // check if the env file exists
                Console.WriteLine("Checking for env file...");
                env = new shellEnv(Exists(exePath + "/env/env.conf"), exePath);
            }
            

            if (env.getVar("HOME").value == "")
                env.setEnv("HOME", exePath);
            
            if (env.getVar("LOAD_SETUP_SCRIPT").value == "1" && args.Length != 1)
            {
                // load the startup file
                string home = env.getVar("HOME").value;
                scriptHandler(home + "/startup.c69");
            }

            if (args.Length == 1)
            {
                // run shell script
                scriptHandler(args[0]);
                return;
            }
            
            // this should be in a while loop
            while(isAlive){
                env.setEnv("PWD", Directory.GetCurrentDirectory());
                updatePrompt();
                Console.Write(env.getVar("PROMPT").value);
                
                string input = Console.ReadLine();
                input = split(input, '#')[0].Trim(); // remove comments

                List<string> tasks = split(input, ';');
                foreach (string task in tasks)
                {
                    try{
                        taskHandler(split(task.Trim(),' '));
                        env.setEnv("taskCount", (int.Parse(env.getVar("taskCount").value) + 1).ToString());
                        env.taskHistory.Add(task.Trim());
                        env.setEnv("lastTaskExitCode", "0");
                        
                    } catch(Exception e){
                        Console.WriteLine(e.Message);
                        env.setEnv("lastTaskExitCode", "1");
                    }
                }
            }
        }
        

        static void callFunction(envFunction func, List<string> args)
        {
            if (func.numArgs != args.Count && env.getVar("useNullArgs").value != "1")
                throw new Exception(String.Format("Function {0} expects {1} arguments, but {2} were given", func.name, func.numArgs, args.Count));
            
            else
                for (int i = 0; i < func.numArgs - args.Count; i++)
                    args.Add("NULL");

            // check if the number of arguments is correct
            if (func.numArgs > 0)
            {
                for (int i = 0; i < func.numArgs; i++)
                    env.setEnv(func.argNames[i], args[i]);
            }

            string funcReturnCode = String.Format("{0}_return", func.name);

            env.setEnv("lastTaskExitCode", "-9999999999999999999999");

            foreach(string line in func.code)
            {
                taskHandler(split(line, ' '));
                if (env.getVar("lastTaskExitCode").value != "-9999999999999999999999")
                    break;
            }

            //if (env.getVar("lastTaskExitCode").value != "0" && env.getVar("lastTaskExitCode").value != "null")
            //    throw new Exception(String.Format("Function {0} exited with exit code {1}", func.name, env.getVar("lastTaskExitCode").value));

            if (func.numArgs == 0)
                return;
            
            foreach (string arg in func.argNames)
                env.removeVar(arg);
        }

        static void updatePrompt() {
            if (!env.funcExists("setPrompt"))
                throw new Exception("setPrompt() function not found");
            envFunction func = env.getFunction("setPrompt");
            callFunction(func, new List<string>());
        }
        
        static void scriptHandler(string scriptFile)
        {
            int lastFilePos = filePos;
            List<string> lastScript = currentFile;
            filePos = 0;
            // check if the file exists
            if ( !fileExists(scriptFile) )
                throw new Exception(String.Format("File {0} does not exist", scriptFile));
            // read the file
            currentFile = readFile(scriptFile);
            // remove comments
            for (int i = 0; i < currentFile.Count; i++)
            {
                // remove #
                currentFile[i] = split(currentFile[i], '#')[0].Trim();
            }
            while (filePos < currentFile.Count)
            {
                if (env.getVar("DEBUG_MODE").value == "1")
                    Console.WriteLine(scriptFile + "<" + filePos + "> :: " + currentFile[filePos]);
                try
                {
                    taskHandler(split(currentFile[filePos], ' '));
                    env.setEnv("taskCount", (int.Parse(env.getVar("taskCount").value) + 1).ToString());
                    env.taskHistory.Add(currentFile[filePos]);
                    env.setEnv("lastTaskExitCode", "0");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    env.setEnv("lastTaskExitCode", "1");
                }
                    
                filePos++;
            }
            filePos = lastFilePos;
            currentFile = lastScript;
        }

        static List<string> taskParse(List<string> task_data) {
            List<string> retTaskData = new List<string>();
            bool inString = false;
            string currentString = "";
            if (task_data.Count == 0)
                return retTaskData;
            // don't do anything to these (will cause issues otherwise)
            if (task_data[0] == "set-alias" || task_data[0] == "func")
                return task_data;

            for(int i = 0; i < task_data.Count; i++)
            {
                if (task_data[i].Contains("\"") || inString)
                {
                    if (task_data[i].Contains("\""))
                    {
                        inString = !inString;
                        if (task_data[i].EndsWith("\""))
                            inString = false;
                        
                        currentString += task_data[i].Replace("\"", "") + " ";
                        if (!inString) {
                            retTaskData.Add(currentString);
                            currentString = "";
                        }
                    }
                    else
                    {
                        currentString += task_data[i] + " ";
                    }
                }
                else if (env.aliasExists(task_data[i]))
                {
                    retTaskData.Add(env.getAlias(task_data[i]));
                }

                else if (task_data[0] != "call" && env.funcExists(task_data[i]))
                {
                    retTaskData.Insert(0, "call");
                    retTaskData.Add(task_data[i]);
                }
                else if (task_data[i].StartsWith("$"))
                {
                    task_data[i] = task_data[i].Substring(1);
                    if (task_data[i].Contains("$"))
                    {
                        List<string> tmp = split(task_data[i], '$');
                        if (env.varExists(tmp[0]))
                        {
                            retTaskData.Add(env.getVar(tmp[0]).value + tmp[1].Replace("$", ""));
                            continue;
                        }
                        
                        retTaskData.Add(task_data[i]);
                        
                        continue;
                    }
                    if (env.varExists(task_data[i]))
                        retTaskData.Add(env.getVar(task_data[i]).value);
                    
                    else
                        retTaskData.Add(task_data[i]);
                    
                }

                else
                    retTaskData.Add(task_data[i]);
                
            }
            return retTaskData;
        }

        static void taskHandler(List<string> task){
            // check the end of logicChain
            if (task.Count == 0 ) { return; }
            if (task[0]    == "") { return; }
            task = taskParse(task);
            if (logicChain.Count > 0 && !logicChain[logicChain.Count - 1])
            {
                if (task.Contains("end"))
                {
                    logicChain.RemoveAt(logicChain.Count - 1);
                    return;
                }
                else if (task.Contains("else"))
                {
                    logicChain[logicChain.Count - 1] = true;
                    return;
                }
                else if (task.Contains("elif"))
                {
                    task[task.IndexOf("elif")] = "if";
                    logicChain.RemoveAt(logicChain.Count - 1);
                }
                else
                    return;
            }
            // loop through the task
            List<envVar> buffer = new List<envVar>();

            switch (task[0].ToLower()) {
                case "}": break;
                case "func":
                    envFunction func = new envFunction();
                    List<string> funcArgs = new List<string>();
                    func.name = task[1];
                    if (env.funcExists(func.name))
                        env.removeFunction(func.name);
                    // see how many things till {
                    int numArgs = 0;
                    for (int i = 2; i < task.Count; i++)
                    {
                        if (task[i] == "{")
                        {
                            numArgs = i - 2;
                            break;
                        }
                        funcArgs.Add(task[i]);
                    }
                    func.argNames = funcArgs;
                    func.numArgs = numArgs;
                    // get the lines between { and }
                    List<string> funcLines = new List<string>();
                    // get the lines between { and }
                    
                    filePos++;
                    for (int i = filePos; i < currentFile.Count; i++)
                    {
                        if (currentFile[i].Trim() == "}")
                        {
                            filePos = i;
                            break;
                        }
                        funcLines.Add(currentFile[i]);
                    }

                    func.code = funcLines;
                    env.addFunction(func.name, func);           
                    break;

                case "elif": break;
                case "else": 
                    logicChain[logicChain.Count - 1] = false;
                    break;

                case "device-name":
                    buffer.Add(new envVar(){name = "device-name", value = deviceName()});
                    break;

                case "device-uname":
                    buffer.Add(new envVar() { name = "device-uname", value = userName() });
                    break;

                case "load-dll":
                    pmgr.loadDll(task[1]);
                    break;

                case "dll-function-call":
                    string[] dllArgs = new string[32];
                                        
                    // get the remaining args
                    for (int i = 3; i < task.Count; i++) {
                        dllArgs[i - 1] = task[i]; 
                    }
                    pmgr.callFunction(task[1], task[2], env, dllArgs);
                    break;


                case "block":
                    envBlock block = new envBlock();
                    block.name = task[1];
                    if (env.blockExists(block.name))
                        env.removeBlock(block.name);
                    List<string> blockCode = new List<string>();
                    // loop through the lines
                    for (int i = filePos + 1; i < currentFile.Count; i++)
                    {
                        if (currentFile[i].Trim() == "block-end")
                        {
                            filePos = i;
                            break;
                        }
                        blockCode.Add(currentFile[i]);
                    }
                    // check the last line for a goto-end
                    if (blockCode[blockCode.Count - 1].Trim() != "goto-end")
                        blockCode.Add("goto-end");
                    
                    block.code = blockCode;
                    env.setBlock(block.name, block);
                    break;

                case "inc-var":
                    if (task.Count < 2)
                        throw new Exception("inc-var requires 1 arguments");
                    if (!env.varExists(task[1]))
                        throw new Exception(String.Format("inc-var: {0} does not exist", task[1]));

                    envVar tmp = env.getVar(task[1]); 
                    
                    if (tmp.type == (int) types.intType)
                        tmp.value = (int.Parse(tmp.value) + 1).ToString();
                    else if (tmp.type == (int) types.floatType)
                        tmp.value = (float.Parse(tmp.value) + 1).ToString();
                    else
                        throw new Exception(String.Format("inc-var: {0} is not an int or float", task[1]));
                    break;

                case "run-sleep":
                    if (task.Count < 2)
                        throw new Exception("run-sleep requires 1 arguments");
                    if (!env.varExists(task[1]))
                        throw new Exception(String.Format("run-sleep: {0} does not exist", task[1]));

                    envVar tmp2 = env.getVar(task[1]);
                    if (tmp2.type != (int) types.intType)
                        throw new Exception(String.Format("run-sleep: {0} is not an int", task[1]));

                    Thread.Sleep(int.Parse(tmp2.value));
                    break;

                case "math":
                    if (task.Count < 4)
                        throw new Exception("math requires 3 arguments");
                    
                    // check if task[2] and task[3] are ints/floats
                    bool task2Int   = int.TryParse(task[2], out int tmpInt);
                    bool task3Int   = int.TryParse(task[3], out int tmpInt2);
                    
                    bool task2Float = float.TryParse(task[2], out float tmpFloat);
                    bool task3Float = float.TryParse(task[3], out float tmpFloat2);

                    if (!task2Int && !task2Float)
                        throw new Exception(String.Format("math: {0} is not an int or float", task[2]));

                    if (!task3Int && !task3Float)
                        throw new Exception(String.Format("math: {0} is not an int or float", task[3]));

                    switch (task[1])
                    {
                        case "add":
                            if (task2Int && task3Int)
                                buffer.Add(new envVar(){name = "math-add", value = (tmpInt + tmpInt2).ToString(), type = (int) types.intType});
                            else if (task2Float && task3Float)
                                buffer.Add(new envVar(){name = "math-add", value = (tmpFloat + tmpFloat2).ToString(), type = (int) types.floatType});
                            else
                                throw new Exception(String.Format("math: {0} and {1} are not the same type", task[2], task[3]));
                            break;

                        case "sub":
                            if (task2Int && task3Int)
                                buffer.Add(new envVar(){name = "math-sub", value = (tmpInt - tmpInt2).ToString(), type = (int) types.intType});
                            else if (task2Float && task3Float)
                                buffer.Add(new envVar(){name = "math-sub", value = (tmpFloat - tmpFloat2).ToString(), type = (int) types.floatType});
                            else
                                throw new Exception(String.Format("math: {0} and {1} are not the same type", task[2], task[3]));
                            break;
                        
                        case "mul":
                            if (task2Int && task3Int)
                                buffer.Add(new envVar(){name = "math-mul", value = (tmpInt * tmpInt2).ToString(), type = (int) types.intType});
                            else if (task2Float && task3Float)
                                buffer.Add(new envVar(){name = "math-mul", value = (tmpFloat * tmpFloat2).ToString(), type = (int) types.floatType});
                            else
                                throw new Exception(String.Format("math: {0} and {1} are not the same type", task[2], task[3]));
                            break;

                        case "div":
                            if (task2Int && task3Int)
                                buffer.Add(new envVar(){name = "math-div", value = (tmpInt / tmpInt2).ToString(), type = (int) types.intType});
                            else if (task2Float && task3Float)
                                buffer.Add(new envVar(){name = "math-div", value = (tmpFloat / tmpFloat2).ToString(), type = (int) types.floatType});
                            else
                                throw new Exception(String.Format("math: {0} and {1} are not the same type", task[2], task[3]));
                            break;
                        
                        default:
                            throw new Exception(String.Format("math: {0} is not a valid operator", task[1]));
                    }
                    break;

                case "goto":
                    if (task.Count > 1)
                    {
                        if (env.blockExists(task[1]))
                        {
                            envBlock tmpBlock = env.getBlock(task[1]);
                            foreach(string line in tmpBlock.code)
                            {
                                Console.WriteLine(line);
                                if (line.Trim() == "goto-end")
                                    break;
                                taskHandler(split(line, ' '));
                            }
                            
                        }
                        else
                            throw new Exception("goto: " + task[1] + " does not exist");
                        
                    } else 
                        throw new Exception("Error: goto requires a label");
                    
                    break;

                case "call":
                    // call a function
                    if (task.Count < 2)
                        throw new Exception("No function name given");
                    if (env.funcExists(task[1]))
                    {
                        // get the function
                        envFunction f = env.getFunction(task[1]);
                        // get the arguments
                        List<string> fargs = new List<string>();
                        if(f.numArgs > 0)
                        {
                            if (task.Count - 2 < f.numArgs && env.getVar("useNullArgs").value != "1")
                                throw new Exception("Not enough arguments given");

                            for (int i = 2; i < task.Count; i++)
                            {
                                fargs.Add(task[i]);
                            }
                        }

                        //Console.WriteLine("Calling function " + task[1]);
                        //Console.WriteLine("Arguments:\n" + String.Join("\n", fargs));

                        callFunction(f, fargs);
                    }
                    break;
                
                case "exec":
                    // TODO: make it so ctrl-c can be used to stop the program but still return to the shell
                    string args = "";
                    if (task.Count > 2)
                    {
                        for (int i = 2; i < task.Count; i++)
                            args += task[i] + " ";
                    }

                    runExe(task[1], args);
                    break;

                case "exec-plugin":
                    System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
                    string result = "";
                    start.FileName = task[1];
                    // join the rest of the arguments
                    string pluginArgs = "";
                    for (int i = 2; i < task.Count; i++)
                        pluginArgs += task[i] + " ";
                    start.Arguments = pluginArgs.Trim();
                    start.UseShellExecute = false;
                    start.RedirectStandardOutput = true;
                    start.RedirectStandardError = true;
                    start.CreateNoWindow = true;
                    start.UseShellExecute = false;

                    using (System.Diagnostics.Process proc = System.Diagnostics.Process.Start(start))
                    {
                        using (StreamReader reader = proc.StandardOutput)
                        {
                            result = reader.ReadToEnd();
                            Console.WriteLine(result);
                        }
                    }

                    if (result.Contains("error"))
                        throw new Exception(result);

                    if (result.Contains("execute:"))
                    {
                        result = result.Replace("execute:", "");
                        if (result.Contains(";"))
                        {
                            foreach (string s in result.Split(';'))
                            {
                                if (s.Trim() != "")
                                    taskHandler(split(s, ' '));
                            }
                        }
                        else
                            taskHandler(split(result, ' '));
                    }


                    break;

                case "remfunc":
                    if (task.Count < 2)
                        throw new Exception("No function name given");
                    if (env.funcExists(task[1]))
                        env.removeFunction(task[1]);
                    
                    break;

                case "check-is":
                    if (task.Count < 3)
                        throw new Exception("No variable name given");
                    envVar v = new envVar();
                    v.name = task[1];

                    switch(task[1]){
                        case "null":
                            v.value = task[2] == "NULL" ? "1" : "0";
                            break;

                        case "empty":
                            v.value = task[2] == "" ? "1" : "0";
                            break;

                        case "equal":
                            v.value = task[2] == task[3] ? "1" : "0";
                            break;
                        
                        case "varExists":
                            v.value = env.varExists(task[2]) ? "1" : "0";
                            break;

                        case "file":
                            v.value = File.Exists(task[2]) ? "1" : "0";
                            break;

                        case "dir":
                            v.value = Directory.Exists(task[2]) ? "1" : "0";
                            break; 

                        case "notequal":
                            v.value = task[2] != task[3] ? "1" : "0";
                            break;

                        case "greater":
                            v.value = Convert.ToInt32(task[2]) > Convert.ToInt32(task[3]) ? "1" : "0";
                            break;

                        case "less":
                            v.value = Convert.ToInt32(task[2]) < Convert.ToInt32(task[3]) ? "1" : "0";
                            break;

                        case "greater-equal":
                            v.value = Convert.ToInt32(task[2]) >= Convert.ToInt32(task[3]) ? "1" : "0";
                            break;

                        case "less-equal":
                            v.value = Convert.ToInt32(task[2]) <= Convert.ToInt32(task[3]) ? "1" : "0";
                            break;

                        case "in":
                            v.value = task[2].Contains(task[3]) ? "1" : "0";
                            break;
                    }
                    // check if '->' is used
                    if (task.Contains("->"))
                    {
                        buffer.Add(v);
                        break;
                    }
                    Console.WriteLine(v.value);
                    break;

                case "set-alias":
                    if (task.Count < 3)
                        throw new Exception("No alias name given");
                    if (env.aliasExists(task[1]))
                        env.removeAlias(task[1]);
                    env.addAlias(task[1], String.Join(" ", task.GetRange(2, task.Count - 2)));
                    break;

                case "user-input":
                    string input = Console.ReadLine();
                    if (task.Count == 1)
                        Console.WriteLine(input);
                    else if (task.Count > 1)
                        env.setEnv(task[1], input);
                    
                    break;

                case "load":
                    if (task.Count <= 1)
                        throw new Exception("load: missing file name");
                    scriptHandler(task[1]);
                    break;

                case "rem":
                    if (task.Count <= 1)
                        throw new Exception("rem: missing variable name");
                    env.removeVar(task[1]);
                    break;

                case "env":
                    if (task.Count > 1)
                    {
                        switch (task[1])
                        {
                            case "out":
                                env.writeEnvFile(env.getVar("ENV_FILE").value);
                                break;
                            case "read":
                                env.readEnvFile(env.getVar("ENV_FILE").value);
                                break;
                            case "func":
                                // dispaly all functions
                                foreach (string funcName in env.getFuncNames()){
                                    envFunction f = env.getFunction(funcName);
                                    Console.WriteLine("{0}({1})", f.name, String.Join(", ", f.argNames));
                                    Console.WriteLine("{");
                                    foreach (string line in f.code)
                                        Console.WriteLine("\t" + line);
                                    Console.WriteLine("}");
                                }
                                break;
                            case "alias":
                                // display all aliases
                                foreach (string aliasName in env.getAliasNames())
                                    Console.WriteLine(aliasName + " -> " + env.getAlias(aliasName));
                                break;

                            case "block":
                                // display all blocks
                                foreach (string blockName in env.getBlockNames())
                                {
                                    Console.WriteLine(blockName);
                                    Console.WriteLine("{");
                                    foreach (string line in env.getBlock(blockName).code)
                                        Console.WriteLine("\t" + line);
                                    Console.WriteLine("}");
                                }
                                break;

                            default:
                                throw new Exception("env: " + task[1] + " is not a valid option");
                        }
                        break;
                    }
                    env.listEnv();
                    break;

                case "loop":
                    if (task.Count < 4)
                        throw new Exception("loop: missing arguments");
                    
                    loopStart.Add(int.Parse(task[1]));
                    loopEnd.Add(int.Parse(task[2]));
                    loopStep.Add(int.Parse(task[3]));
                    loopCurrentIndex.Add(int.Parse(task[1]));
                    loopIndex.Add(filePos);

                    break;

                case "loop-end":  
                    if (loopCurrentIndex[loopCurrentIndex.Count - 1] == loopEnd[loopCurrentIndex.Count - 1])
                    {
                        loopCurrentIndex.RemoveAt(loopCurrentIndex.Count - 1);
                        loopEnd.RemoveAt(loopEnd.Count - 1);
                        loopStart.RemoveAt(loopStart.Count - 1);
                        loopStep.RemoveAt(loopStep.Count - 1);
                        loopIndex.RemoveAt(loopIndex.Count - 1);
                        break;
                    }
                    loopCurrentIndex[loopCurrentIndex.Count - 1] += loopStep[loopCurrentIndex.Count - 1];
                    filePos = loopIndex[loopIndex.Count - 1];
                    
                    break;

                case "file-read":
                    List<string> lines = readFile(task[1]);
                    if (!task.Contains("->"))
                    {
                        foreach (string line in lines)
                            Console.WriteLine(line);
                        
                        break;
                    }
                    string varName = task[2];
                    string slines = "";
                    foreach (string line in lines)
                        slines += line + "\n";
                    
                    buffer.Add(new envVar() { name = varName, value = slines, type = (int)types.stringType, isReadOnly = false });
                    break;

                case "rename":
                    if(task[1].Contains("/")){
                        if(task[2].Contains("/")){
                            File.Copy(task[1],task[2]);
                            File.Delete(task[1]);
                        }else{
                            
                            goto stderr;
                        }
                    }else{    
                        File.Copy(env.getVar("PWD").value+ "\\" + task[1],env.getVar("PWD").value+ "\\" + task[2]);
                        File.Delete(env.getVar("PWD").value+ "\\" + task[1]);
                    }
                    break;
                    
                case "delete":
                    if (task.Count <= 1)
                        throw new Exception("delete: missing file name");
                    else if(task[1].Contains("env.conf"))
                        goto stderr;
                    else{
                        File.Delete(task[1]);

                    }
                    break;

                case "copy":
                    if(task[1].Contains("\\"))
                        File.Copy(task[1],task[2]);
                    else
                        File.Copy(env.getVar("PWD").value+ "\\" + task[1],task[2]);
                    break;

                case "move":
                    if (task.Count < 3)
                        throw new Exception("move: missing file name");
                    if (fileExists(task[1]))
                    {
                        if (task[2].Contains("\\"))
                            File.Move(task[1], task[2]);
                        else
                            File.Move(env.getVar("PWD").value + "\\" + task[1], env.getVar("PWD").value + "\\" + task[2]);
                    }
                    else
                        throw new Exception("move: file does not exist");
                    break;
                    
                case "make-directory":
                    if (task.Count <= 1)
                        throw new Exception("make-direcory: missing directory name");
                    Directory.CreateDirectory(task[1]);
                    break;
                
                case "make-file":
                    if (task.Count <= 1)
                        throw new Exception("make-file: missing file name");
                    using (StreamWriter sw = File.CreateText(task[1]))
                    {
                        sw.Write("");
                    }
                    break;

                case "file-write":
                    if (task.Count <= 1)
                        throw new Exception("file-write: missing file name");
                    if (task.Count <= 2)
                        throw new Exception("file-write: missing content");
                    using (StreamWriter sw = File.AppendText(task[1]))
                    {
                        sw.WriteLine(task[2]);
                    }
                    break;

                case "if":
                    // start an if statement
                    if (task.Count < 2)
                        throw new Exception("if: missing condition");
                    
                    if (task[1].Contains("!"))
                        logicChain.Add(task[1].Substring(1) == "0" ? true : false);
                    else
                        logicChain.Add(task[1] == "0" ? false : true);
                    
                    break;

                case "end":
                    if (logicChain.Count == 1)
                        throw new Exception("end: no if statement found");
                    logicChain.RemoveAt(logicChain.Count - 1);
                    break;

                case "item-exists":
                    if (task.Count < 4)
                        throw new Exception("item-exists: missing item name");
                    switch (task[1]) {
                        case "file":
                            env.setEnv(task[3], (File.Exists(task[2]) ? "1" : "0"));
                            break;

                        case "dir":
                            env.setEnv(task[3], (Directory.Exists(task[2]) ? "1" : "0"));
                            break; 
                        case "var":
                            env.setEnv(task[3], (env.varExists(task[2]) ? "1" : "0"));
                            break;
                        case "func":
                            env.setEnv(task[3], env.funcExists(task[2]) ? "1" : "0");
                            break;
                        default:
                            throw new Exception("item-exists: unknown item type");
                    }
                    break;
                
                case "exit-func":
                    env.setEnv("lastTaskExitCode", task[1]);
                    break;

                case "set-pwd":
                    if (task.Count > 1)
                    {
                        if (task[1] == "..")
                            Directory.SetCurrentDirectory(Directory.GetParent(Directory.GetCurrentDirectory()).FullName);
                        
                        else
                            Directory.SetCurrentDirectory(task[1]);
                    } 
                    else
                        Directory.SetCurrentDirectory(env.getVar("HOME").value);
                    break;

                case "type":
                    // get the type of var
                    throw new Exception("type: not implemented");

                case "list-contents":
                    // get a list of files and folders in the current directory
                    if (task.Count > 1)
                        lsCmd(task[1]);
                    
                    else
                        lsCmd("");
                    
                    break;

                case "set":
                    if (task.Count == 3)
                    {
                        env.setEnv(task[1], task[2]);
                        break;
                    }
                    else
                    {
                        bool isReadOnly = false;
                        string value = "";
                        
                        for (int i = 2; i < task.Count; i++)
                        {
                            isReadOnly = task[i] == "True";
                            if (isReadOnly) { break; }
                            value += task[i] + " ";
                        }
                        env.setEnv(task[1], value.Trim(), isReadOnly);
                        break;
                    }

                case "no":
                    Console.WriteLine("No");
                    goto stderr;

                case "std-error":
                    stderr:
                        Console.WriteLine("char(69)");
                        throw new Exception("you're stupid -_-");

                case "print-console":
                    if (task.Count >= 2)
                    {
                        string output = "";
                        // join the rest of the arguments
                        for (int i = 1; i < task.Count; i++)
                        {
                            if (task[i] == "->") { break;}
                            output += task[i] + " ";
                        }
                        Console.Write(enableEscapeChars(output));
                        buffer.Add(new envVar(){ name = "print-console", value = output, type = (int)types.stringType, isReadOnly = true});
                    }
                    else
                        throw new Exception("print-console: too few arguments");
                    break;

                case "exit":
                    isAlive = false;
                    break;

                case "clear":
                    Console.Clear();
                    break;

                case "find":
                    break;
                
                case "history":
                    int getIndex = 0;
                    if (task.Count > 1)    
                    {
                        if (task[1].GetType() == typeof(string) && task[1] == "all")
                        {
                            for (int i = 0; i < env.taskHistory.Count; i++)
                            {
                                Console.WriteLine(env.taskHistory[i]);
                            }
                            break;
                        }
                        getIndex = int.Parse(task[1]);
                    }
                    if (getIndex < env.taskHistory.Count)
                    {
                        Console.WriteLine(env.taskHistory[getIndex]);
                        buffer.Add(new envVar() { name = "task", value = env.taskHistory[getIndex], type = (int)types.stringType });
                        break;
                    }
                    Console.WriteLine("history: no such task");
                    buffer.Add(new envVar() { name = "task", value = "history: no such task", type = (int)types.stringType });
                    break;

                default:
                    // check if the task is in the env
                    if (env.varExists(task[0]))
                        buffer.Add(env.getVar(task[0]));
                    else
                        throw new Exception("Unknown command: " + task[0]);
                    break;
            }

            // check if '->' is in the task
            if (task.Contains("->"))
            {
                // get the index of '->'
                int index = task.IndexOf("->") + 1;
                
                if (index < task.Count)
                {
                    // check if there is anything in the buffer 
                    if (buffer.Count > 0)
                        // set the value of the variable
                        env.setEnv(task[index], buffer[0].value);
                    
                    else
                        // set the value of the variable
                        env.setEnv(task[index], "NULL");
                    return; 
                } else {
                    Console.WriteLine("Error: there is nothing after '->'.");
                }
            }
        }

    }
}
