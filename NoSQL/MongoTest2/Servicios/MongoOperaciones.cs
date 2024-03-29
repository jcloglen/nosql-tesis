﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NoSQL.Servicios;
using NoSQL.Modelo;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using NoSQL.Helpers;

namespace NoSQL.Servicios
{
    public class MongoOperaciones : IOperaciones
    {
        MongoClient client;
        MongoServer server;
        MongoDatabase db;

        /// <summary>
        /// Identidad de la base de datos (definir en webconfig mas apropiado)
        /// </summary>
        public string Identidad ()
        {          
            return "MongoDB";            
        }

        /// <summary>
        /// Inicializar base de datos
        /// </summary>
        public MongoOperaciones(string dbname, string host, string user = "", string pass = "")
        {
            client = new MongoClient("mongodb://" + host + "/?readPreference=nearest");
            server = client.GetServer();
            db = server.GetDatabase(dbname);
            try
            {
                server.Connect();
            }
            catch (Exception)
            {
            }
        }

        public MongoOperaciones(string dbname, string[] hosts, string user = "", string pass = "")
        {
            string connstr = "mongodb://" + construirReplSetConn(hosts);
            client = new MongoClient(connstr);
            server = client.GetServer();
            db = server.GetDatabase(dbname);
            try
            {
                server.Connect();
            }
            catch (Exception)
            {
            }
        }

        public List<Author> GetAuthors(int skip = 0, int take = 0)
        {

            MongoCollection<Author> col = db.GetCollection<Author>("authors");
            MongoCursor<Author> autores = col.FindAll();
            //OJO! optimización, no trae la foto cuando pedís muchos autores
            autores.SetFields("_id", "Name");
            if (skip != 0)
                autores = autores.SetSkip(skip);
            if (take != 0)
                autores = autores.SetLimit(take);
            return autores.SetSortOrder("Name").ToList();
        }

        public List<Comment> GetComments(int skip = 0, int take = 0)
        {
            MongoCollection<Comment> col = db.GetCollection<Comment>("comments");
            MongoCursor<Comment> comentarios = col.FindAll();
            if (skip != 0)
                comentarios = comentarios.SetSkip(skip);
            if (take != 0)
                comentarios = comentarios.SetLimit(take);
            return comentarios.ToList();
        }

        public List<Comment> GetChildComments(object Parent_id)
        {
            var query = Query.EQ("Parent_id", ObjectId.Parse(Parent_id.ToString()));
            MongoCollection<Comment> col = db.GetCollection<Comment>("comments");
            MongoCursor<Comment> comentarios = col.Find(query);
            return comentarios.ToList();
        }

        public List<Thread> GetThreads(int skip = 0, int take = 0)
        {
            MongoCollection<Thread> col = db.GetCollection<Thread>("threads");
            MongoCursor<Thread> threads = col.FindAll();
            if (skip != 0)
                threads = threads.SetSkip(skip);
            if (take != 0)
                threads = threads.SetLimit(take);
            return threads.ToList();
        }

        public Comment AddComment(Comment comentario)
        {
            comentario.Id = ObjectId.GenerateNewId();
            comentario.Thread_id = ObjectId.Parse(comentario.Thread_id.ToString());
            comentario.Parent_id = ObjectId.Parse(comentario.Parent_id.ToString());
            comentario.CommentCount = 0;
            db.GetCollection<Comment>("comments").Insert(comentario);
            string colPadre = "";
            if (comentario.Thread_id.Equals(comentario.Parent_id))
                colPadre = "threads";
            else
                colPadre = "comments";
            db.GetCollection(colPadre).Update(Query.EQ("_id", new ObjectId(comentario.Parent_id.ToString())), Update.Inc("CommentCount", 1));
            return comentario;
        }

        public Author AddAuthor(Author autor)
        {
            autor.Id = ObjectId.GenerateNewId();
            db.GetCollection<Author>("authors").Insert(autor);
            return autor;
        }

        public Thread AddThread(Thread thread)
        {
            thread.Id = ObjectId.GenerateNewId();
            thread.CommentCount = 0;
            db.GetCollection<Thread>("threads").Insert(thread);
            return thread;
        }
        
        public Author GetAuthor(object id)
        {
            Author author = db.GetCollection<Author>("authors").FindOne(Query.EQ("_id", new ObjectId(id.ToString())));
            return author;
        }

        public Thread GetThread(object id)
        {
            Thread thread = db.GetCollection<Thread>("threads").FindOne(Query.EQ("_id", new ObjectId(id.ToString())));
            return thread;
        }

        public Comment GetComment(object id)
        {
            Comment comment = db.GetCollection<Comment>("comments").FindOne(Query.EQ("_id", new ObjectId(id.ToString())));
            return comment;
        }

        public bool IsDatabaseConnected()
        {
            return server.State == MongoServerState.Connected; 
        }

        public string ConnectionState()
        {
            return server.State.ToString();
        }


        public long GetAuthorsCount()
        {
            return db.GetCollection("authors").Count();
        }

        public long GetThreadsCount()
        {
            return db.GetCollection("threads").Count();
        }

        public long GetCommentsCount()
        {
            return db.GetCollection("comments").Count();
        }

        public bool RemoveAuthor(Author autor)
        {
            var coms = db.GetCollection<Comment>("comments").Find(Query.EQ("author._id", new ObjectId(autor.Id.ToString())));
            foreach (Comment c in coms)
                RemoveComment(c);
            var ths = db.GetCollection<Thread>("threads").Find(Query.EQ("author._id", new ObjectId(autor.Id.ToString())));
            foreach (Thread t in ths)
                RemoveThread(t);
            throw new NotImplementedException();
        }

        public bool RemoveThread(Thread thread)
        {
            db.GetCollection<Comment>("comments").Remove(Query.EQ("thread_id", new ObjectId(thread.Id.ToString())));
            var res = db.GetCollection<Thread>("threads").Remove(Query.EQ("_id", new ObjectId(thread.Id.ToString())));
            return true;
        }

        public bool RemoveComment(Comment comentario)
        {
            //Eliminación recursiva
            var hijos = db.GetCollection<Comment>("comments").Find(Query.EQ("parent_id", new ObjectId(comentario.Id.ToString())));
            foreach (Comment c in hijos)
                RemoveComment(c);
            string colPadre = "";
            if (comentario.Thread_id.Equals(comentario.Parent_id))
                colPadre = "threads";
            else
            {
                colPadre = "comments";
            }
            db.GetCollection(colPadre).Update(Query.EQ("_id", new ObjectId(comentario.Parent_id.ToString())), Update.Inc("CommentCount", -1));
            var res = db.GetCollection<Comment>("comments").Remove(Query.EQ("_id", new ObjectId(comentario.Id.ToString())));
            return true;
        }
        //TODO: calcular el map reduce de manera manual cada tanto para ahorrar tiempo
        public int ThreadsByAuthor(object id)
        {
            var map =
                @"function() {
                   emit(this.Author, { count : 1 });
                }";

            var reduce =
                @"function(key, emits) {
                    total = 0;
                    for (var i in emits) {
                        total += emits[i].count
                    }
                    return { count : total };
                }";

            var mr = db.GetCollection("threads").MapReduce(map, reduce, MapReduceOptions.SetOutput(new MapReduceOutput("salida")));
            var data = db.GetCollection("salida").FindOne(Query.EQ("_id._id", new ObjectId(id.ToString())));
            if(data == null)
                return 0;
            else
                return (int)data["value"]["count"].AsDouble;
        }

        public List<Author> AuthorsPopular(int cant)
        {
            var map =
                @"function() {
                   emit(this.Author, { count : this.CommentCount });
                }";

            var reduce =
                @"function(key, emits) {
                    total = 0;
                    for (var i in emits) {
                        total += emits[i].count
                    }
                    return { count : NumberLong(total) };
                }";

            MapReduceOutput salida = new MapReduceOutput("popular");
            //salida.Mode = MapReduceOutputMode.Replace;
            var mr = db.GetCollection("threads").MapReduce(
                map, 
                reduce, 
                MapReduceOptions.SetOutput(salida)
                );
            MongoCollection col = db.GetCollection("popular");
            MongoCursor cursor = col.FindAllAs<BsonDocument>();
            var sortBy = SortBy.Descending("value");
            cursor.SetSortOrder(sortBy);
            cursor.SetLimit(cant);
            List<Author> lista = new List<Author>();
            foreach (BsonDocument doc in cursor)
            {
                lista.Add(new Author (){
                    Id = doc["_id"]["_id"],
                    Name = doc["_id"]["Name"].AsString
                });
            }
            return lista;
        }
        public List<Author> AuthorsByName(string name, int max)
        {
            //TODO: caso raro, cuando un nombre es muy corto
            string regex = "/^" + name + "/i";
            MongoCollection<Author> col = db.GetCollection<Author>("authors");
            MongoCursor<Author> autores = col.Find(Query.Matches("Name", regex));
            autores.SetFields("_id", "Name");
            autores.SetSortOrder("Name");
            autores.SetLimit(max);
            return autores.ToList();
        }

        public bool Initialize(bool drop)
        {
            //Nota: Mongo no requiere inicialización, salvo para el sharding
            //TODO: Ver índices para las colecciones
            if (drop)
                db.Drop();
            ShardDB();
            //Éste es uno, necesario por las fotos
            db.GetCollection("authors").EnsureIndex(new IndexKeysBuilder().Ascending("Name"), IndexOptions.SetUnique(false));
            return true;
        }
        private bool ShardDB()
        {
            if (server.Instance.InstanceType != MongoServerInstanceType.ShardRouter)
            {
                return true;
            }
            MongoDatabase db = server.GetDatabase("admin");

            /** Código a ejecutar por consola
            sh.enableSharding("forum")
            sh.shardCollection("forum.comments",{"thread_id":1, "_id":1})
            sh.shardCollection("forum.threads",{"_id":1})
            sh.shardCollection("forum.authors",{"_id":1})
            */

            CommandDocument comandoShard = new CommandDocument();
            comandoShard.Add("enableSharding", "forum");
            CommandResult res = db.RunCommand(comandoShard);

            comandoShard = new CommandDocument();
            comandoShard.Add("shardCollection", "forum.comments");
            BsonDocument doc = new BsonDocument();
            doc.Add("Thread_id", 1);
            doc.Add("_id", 1);
            comandoShard.Add("key", doc);
            res = db.RunCommand(comandoShard);

            comandoShard = new CommandDocument();
            comandoShard.Add("shardCollection", "forum.threads");
            doc = new BsonDocument();
            doc.Add("_id", 1);
            comandoShard.Add("key", doc);
            res = db.RunCommand(comandoShard);

            comandoShard = new CommandDocument();
            comandoShard.Add("shardCollection", "forum.authors");
            doc = new BsonDocument();
            doc.Add("_id", 1);
            comandoShard.Add("key", doc);
            res = db.RunCommand(comandoShard);

            return true;
        }

        public bool Cleanup()
        {
            return Initialize(true);
        }


        public bool Shutdown()
        {
            return true;
        }

        private string construirReplSetConn(string [] hosts)
        {
            string connstr = "";
            for (int i = 0; i < hosts.Count(); i++)
            {
                var row = hosts[i];
                if (!hosts[i].Contains(":"))
                    connstr += hosts[i] + ":27017";
                else
                    connstr += hosts[i];
                if (i < hosts.Count() - 1)
                    connstr += ",";
            }
            return connstr + "/";
        }
    }
}
