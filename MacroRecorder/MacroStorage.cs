using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MacroRecorderPro.Interfaces;
using MacroRecorderPro.Models;

namespace MacroRecorderPro.Core
{
    // SRP - отвечает только за сериализацию/десериализацию
    // DIP - зависит от IMacroRepository
    public class MacroStorage : IMacroStorage
    {
        private readonly IMacroRepository repository;
        private readonly JsonSerializerOptions jsonOptions;

        public MacroStorage(IMacroRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));

            jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }

        public void Save(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            var actions = repository.GetAll();

            if (actions.Count == 0)
                throw new InvalidOperationException("No actions to save");

            var json = JsonSerializer.Serialize(actions, jsonOptions);
            File.WriteAllText(filePath, json);
        }

        public void Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Macro file not found", filePath);

            var json = File.ReadAllText(filePath);
            var actions = JsonSerializer.Deserialize<List<MacroAction>>(json);

            if (actions == null || actions.Count == 0)
                throw new InvalidOperationException("Loaded file is empty or invalid");

            repository.SetAll(actions);
        }
    }
}