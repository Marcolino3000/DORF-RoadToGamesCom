using System;
using System.Collections.Generic;
using Runtime.Scripts.Utility;
using UnityEngine;

namespace Utility
{
    [CreateAssetMenu(menuName = "Utility/LogManager")]
    public class LogManager : ScriptableObject
    {
        public List<string> toExclude;
        
        private ILogHandler defaultLogHandler;

        [InspectorButton("Setup")] public bool setup;

    void OnEnable()
    {
        // Setup();
    }

    private void Setup()
    {
        Debug.Log("loghandler onenable");
        defaultLogHandler = Debug.unityLogger.logHandler;

        Debug.unityLogger.logHandler = new CustomLogHandler(defaultLogHandler, toExclude);
    }

    void OnDisable()
    {
        // if (defaultLogHandler != null)
        // {
        //     Debug.unityLogger.logHandler = defaultLogHandler;
        // }
    }

    private class CustomLogHandler : ILogHandler
    {
        private ILogHandler originalHandler;
        private List<string> filters;

        public CustomLogHandler(ILogHandler original, List<string> filters)
        {
            this.originalHandler = original;
            this.filters = filters;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            // Create the full message to check its content
            string fullMessage = String.Format(format, args);

            // Check if the message contains any of the forbidden strings
            foreach (string filter in filters)
            {
                if (fullMessage.Contains(filter))
                {
                    // If found, return immediately. This effectively "suppresses" the message.
                    return; 
                }
            }

            // If we didn't find a match, pass the message to the default Unity handler
            originalHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            // Always pass exceptions through (modifying this is risky)
            originalHandler.LogException(exception, context);
        }
    }
    
    }
}