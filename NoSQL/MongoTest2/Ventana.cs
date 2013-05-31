﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDB.Driver.Builders;

namespace MongoTest2
{
    public partial class Ventana : Form
    {
        MongoClient client;
        MongoServer server;
        MongoDatabase db;
        public Ventana()
        {
            InitializeComponent();
        }

        private void serverState()
        {
            comboBoxShardList.Items.Clear();
            comboBoxShardList.Items.Add("Global");
            foreach (var sh in server.GetDatabase("config").GetCollection("shards").FindAll())
            {
                comboBoxShardList.Items.Add(new ComboItem { Text = sh["_id"], Value = sh["host"] }); ;
            }
            comboBoxShardList.SelectedIndex = 0;
            textBoxEstado.Text = server.State.ToString();
            detalles("Global");
        }

        private void buttonActualizarMonitor_Click(object sender, EventArgs e)
        {
            serverState();
        }

        private void detalles(string referencia)
        {
            CommandDocument comandoStats = new CommandDocument();
            comandoStats.Add("dbstats", 1);
            comandoStats.Add("scale", 1024*1024); //Mb!!
            CommandResult stats = db.RunCommand(comandoStats);
            /**
            CommandDocument comandoSI = new CommandDocument();
            comandoSI.Add("$eval", "printShardingStatus()");
            CommandResult shardingInfo = db.RunCommand(comandoSI);
             **/
            MongoCollection<BsonDocument> chunks = server.GetDatabase("config").GetCollection("chunks");
            if (referencia == "Global")
            {
                labelChunks.Text = "Chunks: " +
                    chunks.Count();
                labelTam.Text = "Tamaño: " +
                    stats.Response["dataSize"]+" Mb";
            }
            else
            {
                labelChunks.Text = "Chunks: " +
                    chunks.Find(new QueryDocument("shard", referencia)).Count();
                labelTam.Text = "Tamaño: " +
                    stats.Response["raw"][((ComboItem)comboBoxShardList.SelectedItem).Value.AsString]["dataSize"] + " Mb";
            }
        }

        private void comboBoxShardList_SelectedIndexChanged(object sender, EventArgs e)
        {
            detalles(comboBoxShardList.SelectedItem.ToString());
        }

        private void buttonAgregarDatos_Click(object sender, EventArgs e)
        {
            Form vd = new VentanaDatos(db);
            vd.ShowDialog();
        }

        private void buttonRandom_Click(object sender, EventArgs e)
        {
            Form vr = new VentanaRandom(db);
            vr.Show();
        }

        private void buttonConectar_Click(object sender, EventArgs e)
        {
            client = new MongoClient("mongodb://localhost");
            server = client.GetServer();
            db = server.GetDatabase("forum");
            serverState();
            if (server.State == MongoServerState.Connected)
                buttonConectar.Enabled = false;
        }
    }
}
