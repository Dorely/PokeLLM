﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.Configuration;

public class QdrantConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334;
    public string ApiKey { get; set; }
    public bool UseHttps { get; set; } = false;
}
