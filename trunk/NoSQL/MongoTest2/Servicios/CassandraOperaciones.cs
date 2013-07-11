﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NoSQL.Servicios;
using NoSQL.Modelo;
using System.Reflection;
using System.Drawing;
using Cassandra;
using Cassandra.Data;

namespace NoSQL.Servicios
{
    public class CassandraOperaciones : IOperaciones
    {

        protected Cluster cluster;
        protected Session session;
        protected string keySpaceName;

        public CassandraOperaciones(string dbname, string host)
        {
            cluster = Cluster.Builder().AddContactPoint(host).Build();
            //session = cluster.Connect(dbname);
            session = cluster.Connect();
            keySpaceName = dbname;
        }
        #region Implementaciones de interfaz

        /*
        public List<Author> GetAuthors(int skip = 0, int take = 0)
        {            
                var authorRows = session.Execute(@"SELECT ""Id"" FROM ""Authors"""); 
                var authors = new List<Author>();
                foreach (Row row in authorRows.GetRows())
                {
                    ConvertToBitmap(row.GetValue<byte[]>("Photo"));
                    var author = new Author();                    
                    author.Id = row.GetValue<Guid>(0);
                    author.Name = row.GetValue<string>("Name");
                    //author.Photo = ConvertToBitmap(row.GetValue<byte[]>("Photo"));
                    author.Photo = new Bitmap(1,1);
                    
                    authors.Add(author);
                }
                return authors.ToList().Skip(skip).ToList();                        
        }
         */

        // Implementación alternativa de GetAuthors
        public List<Author> GetAuthors(int skip = 0, int take = 0)
        {
            var authors = new List<Author>();
            var authorRows = session.Execute(@"SELECT ""Id"" FROM ""Authors""");                        
            var rows = authorRows.GetRows().ToArray();                                       
            if (rows.Count() == 0)                            
                return authors;

            if (skip == 0 && take == 0)
            {
                skip = 0;
                take = rows.Count();
            }
            take += skip;
            while (skip < take)
            {
                var author = new Author();
                author.Id = rows[skip].GetValue<Guid>(0);
                author = this.GetAuthor(author.Id);
                authors.Add(author);
                skip++;
            }
            return authors;                        
        }

        public List<Comment> GetComments(int skip = 0, int take = 0)
        {
            var commentRows = session.Execute(@"SELECT * FROM ""Comments""");
                var comments = new List<Comment>();
                foreach (Row row in commentRows.GetRows())
                {
                    var comment = new Comment()
                    {
                        Author = this.GetAuthor(row.GetValue<Guid>("Author")),
                        CommentCount = 0,
                        Date = row.GetValue<DateTime>("Date"),
                        Id = row.GetValue<Guid>(0),
                        Parent_id = row.GetValue<Guid>("Parent_id"),
                        Thread_id = row.GetValue<Guid>("Thread_id"),
                        Text = row.GetValue<string>("Text"),
                    };
                    comment.CommentCount = GetChildCommentCounts(comment.Id);                         
                    comments.Add(comment);
                }
                return comments.ToList().Skip(skip).ToList();            
        }

        public List<Comment> GetChildComments(object Parent_id)
        {
                var commentRows = session.Execute(@"SELECT * FROM ""Comments"" WHERE ""Parent_id""=" + Parent_id);
                var comments = new List<Comment>();

                foreach (Row row in commentRows.GetRows())
                {
                    var comment = new Comment()
                    {
                        Author = this.GetAuthor(row.GetValue<Guid>("Author")),
                        CommentCount = 0,
                        Date = row.GetValue<DateTime>("Date"),
                        Id = row.GetValue<Guid>(0),
                        Parent_id = row.GetValue<Guid>("Parent_id"),
                        Thread_id = row.GetValue<Guid>("Thread_id"),
                        Text = row.GetValue<string>("Text")
                    };
                    comments.Add(comment);
                }
                return comments.ToList();            
        }

        public List<Thread> GetThreads(int skip = 0, int take = 0)
        {
            var threadRows = session.Execute(@"SELECT * FROM ""Threads""");
            // TODO: no se logran mapear el SET de tag, la consulta de abajo falla
            // var threadRows = db.ExecuteQuery(@"SELECT * FROM ""Threads""").ToList();                       
            var threads = new List<Thread>();
            foreach (Row row in threadRows.GetRows())
            {
                var thread = new Thread()
                {
                    Author = this.GetAuthor(row.GetValue<Guid>("Author")),
                    CommentCount = 0,
                    Date = row.GetValue<DateTime>("Date"),
                    Id = row.GetValue<Guid>(0),
                    Title = row.GetValue<string>("Title"),                    
                };
                if (row.GetValue<List<string>>("Tags") != null)
                    thread.Tags = row.GetValue<List<string>>("Tags").ToArray();
                threads.Add(thread);
            }
            return threads.Skip(skip).ToList();
        }

        public Thread GetThread(object id)
        {            
                var threadRows = session.Execute(@"SELECT * FROM ""Threads"" WHERE ""Id""=" + id).GetRows();
                Row row = threadRows.First();
                var thread = new Thread()
                {
                    Author = this.GetAuthor(row.GetValue<Guid>("Author")),
                    CommentCount = 0,
                    Date = row.GetValue<DateTime>("Date"),
                    Id = row.GetValue<Guid>(0),
                    Title = row.GetValue<string>("Title"),                    
                };
                if (row.GetValue<List<string>>("Tags") != null)
                    thread.Tags = row.GetValue<List<string>>("Tags").ToArray();
                thread.CommentCount = GetChildCommentCounts(id);
                return thread;               
        }

        public Author GetAuthor(object id)
        {
            var result = session.Execute(@"SELECT * FROM ""Authors"" WHERE ""Id""="+id).GetRows();
            var author = new Author();
            Row row = result.First();
            row.GetValue<Guid>(0);
            row.GetValue<string>("Name");
            ConvertToBitmap(row.GetValue<byte[]>("Photo"));
            author = new Author()
            {
                Id = row.GetValue<Guid>(0),
                Name = row.GetValue<string>("Name"),
                Photo = ConvertToBitmap(row.GetValue<byte[]>("Photo"))

            };
            return author;
        }

        public Comment GetComment(object id)
        {
            var commentRows = session.Execute(@"SELECT * FROM ""Comments"" WHERE ""Id""=" + id).GetRows();
            Row row = commentRows.First();
            var comment = new Comment()
                {
                    Author = this.GetAuthor(row.GetValue<Guid>("Author")),
                    CommentCount = 0,
                    Date = row.GetValue<DateTime>("Date"),
                    Id = row.GetValue<Guid>(0),
                    Parent_id = row.GetValue<Guid>("Parent_id"),
                    Thread_id = row.GetValue<Guid>("Thread_id"),
                    Text = row.GetValue<string>("Text")
                };
            comment.CommentCount = GetChildCommentCounts(comment.Id);                         
            return comment;
        }

        public Comment AddComment(Comment comentario)
        {
            // Generar nuevo Id
            Guid id = Guid.NewGuid();

            // Resolver Author Id            
            string AuthorId = comentario.Author.Id.ToString();

            string addStmt = string.Format(getInsertStatementFor("Comment", "NoSQL.Modelo"),
                AuthorId,
                0,
                getDateInMilliseconds(),
                id,
                comentario.Parent_id,
                asCassandraString(comentario.Text),
                comentario.Thread_id
                );
            session.Execute(addStmt);
            //TODO: Actualizar el contador de comentarios del padre!!
            comentario.Id = id;
            IncrCounter("Comment");
            IncrCounterParent(comentario.Parent_id);
            return comentario;
        }

        public Author AddAuthor(Author autor)
        {
            ImageConverter converter = new ImageConverter();
            byte[] bytes = (byte[])converter.ConvertTo(autor.Photo, typeof(byte[]));
            Guid id = Guid.NewGuid();
            string addStmt = string.Format(getInsertStatementFor("Author", "NoSQL.Modelo"),
                id,
                asCassandraString(autor.Name),
                asCassandraString(BitConverter.ToString(bytes).Replace("-", "")));
            session.Execute(addStmt);
            autor.Id = id;
            IncrCounter("Author");
            return autor;
        }

        public Thread AddThread(Thread thread)
        {
            Guid id = Guid.NewGuid();
            string AuthorId = thread.Author.Id.ToString();
            string tags = "";
            foreach (string tag in thread.Tags)
            {
                tags = tags + "'" + tag + "',";
            }
            if (thread.Tags.Count() > 0)
                tags = tags.Remove(tags.Length - 1);
            tags = "[" + tags + "]";
            string addStmt = string.Format(getInsertStatementFor("Thread", "NoSQL.Modelo"),
                AuthorId,
                thread.CommentCount,
                getDateInMilliseconds(),
                id,
                tags,
                asCassandraString(thread.Title)
                );                 
            session.Execute(addStmt);            
            thread.CommentCount = GetChildCommentCounts(id);
            IncrCounter("Thread");            
            thread.Id = id;
            return thread;
        }

        public long GetAuthorsCount()
        {
            var resu = session.Execute(@"SELECT Count(*) FROM ""Authors""").GetRows();
            long num = resu.First().GetValue<long>(0);
            return num;
        }

        public long GetThreadsCount()
        {
            var resu = session.Execute(@"SELECT Count(*) FROM ""Authors""").GetRows();
            long num = resu.First().GetValue<long>(0);
            return num;
        }

        public long GetCommentsCount()
        {
            var resu = session.Execute(@"SELECT Count(*) FROM ""Authors""").GetRows();
            long num = resu.First().GetValue<long>(0);
            return num;
        }

        public bool RemoveAuthor(Author autor)
        {            
                session.Execute(@"DELETE FROM ""Authors"" WHERE Id = " + autor.Id);
                DecrCounter("Author");
                return true;
        }                    

        public bool RemoveThread(Thread thread)
        {
           
                session.Execute(@"DELETE FROM ""Threads"" WHERE ""Id"" = " + thread.Id);
                DecrCounter("Thread");
                return true;            
        }

        public bool RemoveComment(Comment comentario)
        {            
                session.Execute(@"DELETE FROM ""Comments"" WHERE ""Id"" = " + comentario.Id);
                //TODO: Actualizar el contador de comentarios del padre hmm
                DecrCounter("Comment");
                return true;            
        }

        public bool IsDatabaseConnected()
        {
            try
            {
                cluster.Metadata.GetKeyspace(keySpaceName);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public Dictionary<string, string> GetShards()
        {
            var dc = new Dictionary<string, string>();
            dc.Add("1", "localhost");
            return dc;
        }

        public string ConnectionState()
        {
            if (this.IsDatabaseConnected())
                return "Conectado";
            else
                return "Desconectado";
        }

        public string Identidad()
        {
            return "Cassandra";
        }
        #endregion

        #region Helpers


        /// <summary>
        /// Obtener la cantidad de respuestas de un comentario
        /// </summary>
        /// <param name="ParentId"></param>
        /// <returns></returns>
        protected long GetChildCommentCounts(object ParentId)
        {
            var results = session.Execute(@"select * from ""CommentCounts"" where ""Id""=" + ParentId).GetRows().ToList();
            if (results.Count() > 0)
                return results.First().GetValue<long>("count");
            return 0;
        }

        /// <summary>
        /// Incrementar el contador para una entidad
        /// </summary>
        /// <param name="EntityName"></param>
        protected void IncrCounter(string EntityName)
        {
            session.Execute(@"update ""Counters"" set ""count""=""count""+1 where ""name""='" + EntityName + "'");
        }

        /// <summary>
        /// Decrementar el contador para una entidad
        /// </summary>
        /// <param name="EntityName"></param>
        protected void DecrCounter(string EntityName)
        {
            session.Execute(@"update ""Counters"" set ""count""=""count""-1 where ""name""='" + EntityName + "'");
        }

        protected void IncrCounterParent(object parentId)
        {
            session.Execute(@"update ""CommentCounts"" set ""count""=""count""+1 where ""Id""=" + parentId);
        }

        /// <summary>
        /// Obtener un bitmap en base a un array de bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        protected Bitmap ConvertToBitmap(byte[] bytes)
        {
            ImageConverter ic = new ImageConverter();
            Image img = (Image)ic.ConvertFrom(bytes);
            return new Bitmap(img);
        }

        /// <summary>
        /// Convertir String hexadecimal a byte
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        protected static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        /// <summary>
        /// Obtener la fecha actual en milisegundos
        /// </summary>
        /// <returns></returns>
        protected long getDateInMilliseconds()
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            return (long)t.TotalMilliseconds;
        }

        /// <summary>
        /// Crear keyspace de nombre keyspacename
        /// </summary>
        protected void createKeyspace()
        {
            session.CreateKeyspaceIfNotExists(keySpaceName);
        }

        /// <summary>
        /// Crear column families (tablas) en base al Modelo
        /// </summary>
        protected void createColumnFamilies()
        {
            session.Execute("use " + keySpaceName);
            var statements = CassandraCreateStatementsBasedOnModel("NoSQL.Modelo");
            foreach (string statement in statements)
                session.Execute(statement);

            // Coleccion de tags
            //            db.ExecuteNonQuery(@"ALTER TABLE ""Threads"" ADD tags set<text>");

            // Crear indexes
            session.Execute(@"create index comments_parent_id on ""Comments"" (""Parent_id"")");

            // Crear counter column family
            session.Execute(@"create columnfamily ""Counters""(""name"" text primary key, ""count"" counter)");

            session.Execute(@"create columnfamily ""CommentCounts""(""Id"" uuid primary key, ""count"" counter)");            
        }

        /// <summary>
        /// Devolver una lista de sentencias para ejecutar sobre Cassandra donde cada una crea un column family (tabla)
        /// tomando el modelo de clases como referencia 
        /// </summary>
        /// <param name="modelNamespacePath"></param>
        /// <returns>lista de sentencias ejecutables que crean column family</returns>
        protected List<string> CassandraCreateStatementsBasedOnModel(string modelNamespacePath)
        {
            var q = from t in Assembly.GetExecutingAssembly().GetTypes()
                    where t.IsClass && t.Namespace == modelNamespacePath
                    select t;

            List<string> createStatements = new List<string>();
            foreach (Type str in q.ToList())
            {
                string columnFamilyName = str.Name + "s";
                string createStatement = @"create columnfamily """ + columnFamilyName + @""" (";
                int i = 0;
                var properties = str.GetProperties();
                var propertyTypes = new List<string>();
                foreach (PropertyInfo pi in properties)
                {
                    createStatement = createStatement + @"""" + pi.Name + @""" " + typeMapping(pi.PropertyType);
                    if (pi.Name.ToLower() == "id")
                        createStatement = createStatement + " PRIMARY KEY";
                    i++;
                    if (i < properties.Count())
                        createStatement = createStatement + ",";
                    else
                        createStatement = createStatement + ");";
                }
                createStatements.Add(createStatement);
            }
            return createStatements;
        }

        /// <summary>
        /// Resolver el tipo correspodiente a Cassandra dado un tipo de C#
        /// </summary>
        /// <param name="type"></param>
        /// <returns>un tipo cassandra</returns>
        protected string typeMapping(Type type)
        {
            if (type == typeof(Bitmap))
                return "blob";
            if (type == typeof(char) || type == typeof(string))
                return "text";
            if (type == typeof(Int64) || type == typeof(long))
                return "bigint";
            if (type == typeof(int))
                return "int";
            if (type == typeof(float))
                return "float";
            if (type == typeof(double))
                return "double";
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof(DateTime))
                return "timestamp";
            if (type == typeof(string[]))
                return "list<text>";
            if (type == typeof(object) || !(type == typeof(ValueType)))
                return "uuid";

            // por defecto:
            return "text";
        }

        /// <summary>
        /// Devuelve un statement parametrizado para agregar a un column family
        /// </summary>
        /// <param name="columnFamilyName"></param>
        /// <param name="modelNamespacePath"></param>
        /// <returns></returns>
        protected string getInsertStatementFor(string entityName, string modelNamespacePath)
        {
            var q = from t in Assembly.GetExecutingAssembly().GetTypes()
                    where t.IsClass && t.Namespace == modelNamespacePath && t.Name == entityName
                    select t;
            string stmt = @"INSERT INTO """ + entityName + @"s"" (";
            var properties = q.First().GetProperties().OrderBy(p => p.Name.ToUpper());
            int j = 0;
            foreach (PropertyInfo property in properties)
            {
                stmt = stmt + @"""" + property.Name + @"""";
                j++;
                if (j < properties.Count())
                    stmt = stmt + ",";
                else
                    stmt = stmt + ")";
            }
            stmt = stmt + " VALUES (";
            for (int i = 1; i <= properties.Count(); i++)
            {
                stmt = stmt + "{" + (i - 1) + "}";
                if (i < properties.Count())
                    stmt = stmt + ",";
                else
                    stmt = stmt + ");";
            }
            return stmt;
        }

        /// <summary>
        /// Dado un string, devolver entre ''
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        protected string asCassandraString(string str)
        {
            return "'" + str + "'";
        }
        #endregion

        protected bool existsDatabase()
        {
            try
            {
                session.Execute("use " + keySpaceName);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }            
        }

        public bool Initialize(bool dropExistent)
        {
            session.Execute("use system");
            if (dropExistent)
                session.DeleteKeyspaceIfExists(keySpaceName);
            if (!existsDatabase())
            {
                createKeyspace();
                session.Execute("use " + keySpaceName);
                createColumnFamilies();
            }
            cluster.Connect(keySpaceName);
            return true;
        }

        public bool Cleanup()
        {            
                Initialize(true); // JUJUJUJU
                return true;                        
        }


        public bool Shutdown()
        {            
                cluster.Shutdown();
                return true;                        
        }
    }
}