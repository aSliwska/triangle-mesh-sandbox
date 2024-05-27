using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ConfigReader
{
    public string readConnectionString()
    {
        StreamReader sr = new StreamReader(".\\connection.config");
        string connectionString = sr.ReadLine();
        sr.Close();

        return connectionString;
    }
}
