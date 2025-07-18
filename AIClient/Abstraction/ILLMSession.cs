﻿namespace LLMClient.Abstraction
{
    public interface ILLMSession
    {
        DateTime EditTime { get; }

        bool IsBusy { get; }

        Task Backup();

        void Delete();
    }
}