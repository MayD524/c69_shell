
using System.Collections.Generic;
using c69_shellEnv;
using System;

namespace c69_shellCTXMgr 
{
    public class ctxMgr 
    {
        private static List<string> currentContext = new List<string>();
        private shellEnv env;

        public ctxMgr (shellEnv env) 
        {
            this.env = env;
        }

        public string[] getMatchingContexts(string context) 
        {
            List<string> matches = new List<string>();
            foreach (string c in currentContext)
            {
                if (c.StartsWith(context))
                    matches.Add(c);
            }
            return matches.ToArray();
        }

        public string getUserInput()
        {
            int taskHistoryIndex = env.taskHistory.Count - 1;
            string prompt = env.getVar("PROMPT").value;
            int maxBackSpace = prompt.Length;
            int maxRight = maxBackSpace;
            Console.Write(prompt);
            string userInput = "";
            ConsoleKeyInfo keyInfo;
            char currentChar;
            while (true)
            {
                keyInfo = Console.ReadKey(true);
                switch (keyInfo.Key)
                {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        return userInput;
                    
                    case ConsoleKey.Backspace:
                        if (userInput.Length > 0)
                        {
                            userInput = userInput.Substring(0, userInput.Length - 1);
                            Console.Write("\b \b");
                            maxRight--;
                        }
                        break;
                    
                    case ConsoleKey.Tab:
                        string[] matches = getMatchingContexts(userInput);
                        if (matches.Length > 0)
                        {
                            if (matches.Length == 1)
                            {
                                userInput = matches[0].Substring(userInput.Length);
                                Console.Write(userInput);
                                maxRight += matches[0].Substring(userInput.Length).Length;
                            }
                            else
                            {
                                Console.WriteLine();
                                for (int i = 0; i < matches.Length; i++)
                                {
                                    Console.Write("\t" + matches[i]);
                                }
                            }
                        }
                        break;

                    case ConsoleKey.Escape:
                        return "";

                    case ConsoleKey.UpArrow:
                        break;

                    case ConsoleKey.DownArrow:
                        break;

                    case ConsoleKey.RightArrow:
                        Console.SetCursorPosition(Console.CursorLeft + 1, Console.CursorTop);
                        
                        break;
                    
                    case ConsoleKey.LeftArrow:
                        if (userInput.Length > 0 && Console.CursorLeft >= maxBackSpace)
                        {
                            // move cursor 1 char left
                            Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        }
                    
                        break;

                    default:
                        currentChar = keyInfo.KeyChar;
                        if (currentChar != '\0')
                        {
                            userInput += currentChar;
                            Console.Write(currentChar);
                        }
                        break;
                }
            }
        }
    }
}