using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using NewLife;
using NewLife.Log;
using NewLife.Net;

namespace XCode.DataAccessLayer
{
    class SqlServer : RemoteDb
    {
        #region ����
        /// <summary>�������ݿ����͡��ⲿDAL���ݿ�����ʹ��Other</summary>
        public override DatabaseType DbType { get { return DatabaseType.SqlServer; } }

        /// <summary>����</summary>
        public override DbProviderFactory Factory { get { return SqlClientFactory.Instance; } }

        private Boolean? _IsSQL2005;
        /// <summary>�Ƿ�SQL2005������</summary>
        public Boolean IsSQL2005
        {
            get
            {
                if (_IsSQL2005 == null)
                {
                    if (String.IsNullOrEmpty(ConnectionString)) return false;

                    // ����Session���������л����ݿ�����³���
                    var session = OnCreateSession() as DbSession;
                    session.ConnectionString = ConnectionString;
                    session.Database = this;
                    String ver = null;
                    try
                    {
                        ////�л���master��
                        //String dbname = session.DatabaseName;
                        ////���ָ�������ݿ��������Ҳ���master�����л���master
                        //if (!String.IsNullOrEmpty(dbname) && !dbname.EqualIgnoreCase(SystemDatabaseName))
                        //{
                        //    session.DatabaseName = SystemDatabaseName;
                        //}
                        //���ָ�������ݿ��������Ҳ���master�����л���master
                        session.DatabaseName = SystemDatabaseName;

                        //ȡ���ݿ�汾
                        if (!session.Opened) session.Open();
                        ver = session.Conn.ServerVersion;
                        session.AutoClose();

                        //_IsSQL2005 = !ver.StartsWith("08");
                        _IsSQL2005 = Int32.Parse(ver.Substring(0, 2)) > 8;

                        //if (!String.IsNullOrEmpty(dbname) && !dbname.EqualIgnoreCase(SystemDatabaseName))
                        //{
                        //    session.DatabaseName = dbname;
                        //}
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteLine("��ѯ[{0}]�İ汾ʱ����������MSSQL2000���з�ҳ������{1}", ConnName, ex);
                        _IsSQL2005 = false;
                    }
                    finally { session.Dispose(); }

                    //if (DAL.Debug) DAL.WriteLog("[{0}/{1}]�汾��{2}��{3}��MSSQL2000��", ConnName, DbType, ver, _IsSQL2005.Value ? "��" : "");
                }
                return _IsSQL2005.Value;
            }
            //set { _IsSQL2005 = value; }
        }

        private String _DataPath;
        /// <summary>����Ŀ¼</summary>
        public String DataPath
        {
            get { return _DataPath; }
            set { _DataPath = value; }
        }

        const String Application_Name = "Application Name";
        protected override void OnSetConnectionString(XDbConnectionStringBuilder builder)
        {
            String str = null;
            // ��ȡ����Ŀ¼�����ڷ��򹤳̴������ݿ�
            if (builder.TryGetAndRemove("DataPath", out str) && !String.IsNullOrEmpty(str)) DataPath = str;

            base.OnSetConnectionString(builder);

            if (!builder.ContainsKey(Application_Name))
            {
                String name = Runtime.IsWeb ? HostingEnvironment.SiteName : AppDomain.CurrentDomain.FriendlyName;
                builder[Application_Name] = String.Format("XCode_{0}_{1}", name, ConnName);
            }
        }
        #endregion

        #region ����
        /// <summary>�������ݿ�Ự</summary>
        /// <returns></returns>
        protected override IDbSession OnCreateSession() { return new SqlServerSession(); }

        /// <summary>����Ԫ���ݶ���</summary>
        /// <returns></returns>
        protected override IMetaData OnCreateMetaData() { return new SqlServerMetaData(); }

        public override bool Support(string providerName)
        {
            providerName = providerName.ToLower();
            if (providerName.Contains("system.data.sqlclient")) return true;
            if (providerName.Contains("sql2012")) return true;
            if (providerName.Contains("sql2008")) return true;
            if (providerName.Contains("sql2005")) return true;
            if (providerName.Contains("sql2000")) return true;
            if (providerName == "sqlclient") return true;
            if (providerName.Contains("mssql")) return true;

            return false;
        }
        #endregion

        #region ��ҳ
        /// <summary>�����ҳSQL</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <returns>��ҳSQL</returns>
        public override String PageSplit(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            // �ӵ�һ�п�ʼ������Ҫ��ҳ
            if (startRowIndex <= 0 && maximumRows < 1) return sql;

            // ָ������ʼ�У�������SQL2005�����ϰ汾��ʹ��RowNumber�㷨
            if (startRowIndex > 0 && IsSQL2005)
            {
                //return PageSplitRowNumber(sql, startRowIndex, maximumRows, keyColumn);
                SelectBuilder builder = new SelectBuilder();
                builder.Parse(sql);
                return MSPageSplit.PageSplit(builder, startRowIndex, maximumRows, IsSQL2005).ToString();
            }

            // ���û��Order By��ֱ�ӵ��û��෽��
            // �����ַ����жϣ������ʸߣ�����������ߴ���Ч��
            if (!sql.Contains(" Order "))
            {
                if (!sql.ToLower().Contains(" order ")) return base.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
            }
            //// ʹ����������ϸ��жϡ��������Order By���������ұ�û��������)��������order by���Ҳ����Ӳ�ѯ�ģ�����Ҫ���⴦��
            //MatchCollection ms = Regex.Matches(sql, @"\border\s*by\b([^)]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            //if (ms == null || ms.Count < 1 || ms[0].Index < 1)
            String sql2 = sql;
            String orderBy = CheckOrderClause(ref sql2);
            if (String.IsNullOrEmpty(orderBy))
            {
                return base.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
            }
            // ��ȷ����sql����㺬��order by���ټ��������Ƿ���top����Ϊû��top��order by�ǲ�������Ϊ�Ӳ�ѯ��
            if (Regex.IsMatch(sql, @"^[^(]+\btop\b", RegexOptions.Compiled | RegexOptions.IgnoreCase))
            {
                return base.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
            }
            //String orderBy = sql.Substring(ms[0].Index);

            // �ӵ�һ�п�ʼ������Ҫ��ҳ
            if (startRowIndex <= 0)
            {
                if (maximumRows < 1)
                    return sql;
                else
                    return String.Format("Select Top {0} * From {1} {2}", maximumRows, CheckSimpleSQL(sql2), orderBy);
                //return String.Format("Select Top {0} * From {1} {2}", maximumRows, CheckSimpleSQL(sql.Substring(0, ms[0].Index)), orderBy);
            }

            #region Max/Min��ҳ
            // ���Ҫʹ��max/min��ҳ��������keyColumn������asc����desc
            String kc = keyColumn.ToLower();
            if (kc.EndsWith(" desc") || kc.EndsWith(" asc") || kc.EndsWith(" unknown"))
            {
                String str = PageSplitMaxMin(sql, startRowIndex, maximumRows, keyColumn);
                if (!String.IsNullOrEmpty(str)) return str;
                keyColumn = keyColumn.Substring(0, keyColumn.IndexOf(" "));
            }
            #endregion

            sql = CheckSimpleSQL(sql2);

            if (String.IsNullOrEmpty(keyColumn)) throw new ArgumentNullException("keyColumn", "��ҳҪ��ָ�������л��������ֶΣ�");

            if (maximumRows < 1)
                sql = String.Format("Select * From {1} Where {2} Not In(Select Top {0} {2} From {1} {3}) {3}", startRowIndex, sql, keyColumn, orderBy);
            else
                sql = String.Format("Select Top {0} * From {1} Where {2} Not In(Select Top {3} {2} From {1} {4}) {4}", maximumRows, sql, keyColumn, startRowIndex, orderBy);
            return sql;
        }

        public override SelectBuilder PageSplit(SelectBuilder builder, int startRowIndex, int maximumRows)
        {
            return MSPageSplit.PageSplit(builder, startRowIndex, maximumRows, IsSQL2005, b => CreateSession().QueryCount(b));
        }
        #endregion

        #region ���ݿ�����
        /// <summary>��ǰʱ�亯��</summary>
        public override String DateTimeNow { get { return "getdate()"; } }

        /// <summary>��Сʱ��</summary>
        public override DateTime DateTimeMin { get { return SqlDateTime.MinValue.Value; } }

        /// <summary>���ı�����</summary>
        public override Int32 LongTextLength { get { return 4000; } }

        /// <summary>��ȡGuid�ĺ���</summary>
        public override String NewGuid { get { return "newid()"; } }

        /// <summary>��ʽ��ʱ��ΪSQL�ַ���</summary>
        /// <param name="dateTime">ʱ��ֵ</param>
        /// <returns></returns>
        public override String FormatDateTime(DateTime dateTime) { return "{ts" + String.Format("'{0:yyyy-MM-dd HH:mm:ss}'", dateTime) + "}"; }

        /// <summary>��ʽ���ؼ���</summary>
        /// <param name="keyWord">�ؼ���</param>
        /// <returns></returns>
        public override String FormatKeyWord(String keyWord)
        {
            //if (String.IsNullOrEmpty(keyWord)) throw new ArgumentNullException("keyWord");
            if (String.IsNullOrEmpty(keyWord)) return keyWord;

            if (keyWord.StartsWith("[") && keyWord.EndsWith("]")) return keyWord;

            return String.Format("[{0}]", keyWord);
        }

        /// <summary>ϵͳ���ݿ���</summary>
        public override String SystemDatabaseName { get { return "master"; } }

        public override string FormatValue(IDataColumn field, object value)
        {
            TypeCode code = Type.GetTypeCode(field.DataType);
            Boolean isNullable = field.Nullable;

            if (code == TypeCode.String)
            {
                // �������� Hannibal �ڴ���������վʱ���ֲ��������Ϊ���룬�������Nǰ׺
                if (value == null) return isNullable ? "null" : "''";
                if (String.IsNullOrEmpty(value.ToString()) && isNullable) return "null";

                // ����ֱ���ж�ԭʼ���������������ף����ԭʼ���ݿⲻ�ǵ�ǰ���ݿ⣬��ô������жϽ���ʧЧ
                // һ�����еİ취���Ǹ�XField����һ��IsUnicode���ԣ������һ����XField����΢�����
                // Ŀǰ��ʱӰ�첻�󣬺��濴��������Ƿ����Ӱ�
                //if (field.RawType == "ntext" ||
                //    !String.IsNullOrEmpty(field.RawType) && (field.RawType.StartsWith("nchar") || field.RawType.StartsWith("nvarchar")))

                // Ϊ�˼��ݾɰ汾ʵ����
                if (field.IsUnicode || IsUnicode(field.RawType))
                    return "N'" + value.ToString().Replace("'", "''") + "'";
                else
                    return "'" + value.ToString().Replace("'", "''") + "'";
            }
            //else if (field.DataType == typeof(Guid))
            //{
            //    if (value == null) return isNullable ? "null" : "''";

            //    return String.Format("'{0}'", value);
            //}

            return base.FormatValue(field, value);
        }
        #endregion
    }

    /// <summary>SqlServer���ݿ�</summary>
    internal class SqlServerSession : RemoteDbSession
    {
        #region ��ѯ
        /// <summary>���ٲ�ѯ������¼��������ƫ��</summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public override Int64 QueryCountFast(string tableName)
        {
            String sql = String.Format("select rows from sysindexes where id = object_id('{0}') and indid in (0,1)", tableName);
            return ExecuteScalar<Int64>(sql);

            //QueryTimes++;
            //DbCommand cmd = CreateCommand();
            //cmd.CommandText = sql;
            //WriteSQL(cmd.CommandText);
            //try
            //{
            //    return Convert.ToInt64(cmd.ExecuteScalar());
            //}
            //catch (DbException ex)
            //{
            //    throw OnException(ex, cmd.CommandText);
            //}
            //finally { AutoClose(); }
        }

        /// <summary>ִ�в�����䲢���������е��Զ����</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="type">�������ͣ�Ĭ��SQL�ı�</param>
        /// <param name="ps">�������</param>
        /// <returns>�����е��Զ����</returns>
        public override Int64 InsertAndGetIdentity(string sql, CommandType type = CommandType.Text, params DbParameter[] ps)
        {
            return ExecuteScalar<Int64>("SET NOCOUNT ON;" + sql + ";Select SCOPE_IDENTITY()", type, ps);
        }
        #endregion

        #region ���ټ��
        public override void QuickTest()
        {
            QuickConn();
            base.QuickTest();
        }

        /// <summary>
        /// ���ټ��
        /// </summary>
        private void QuickConn()
        {
            Regex regexServer = new Regex(@"Server=(.+?);", RegexOptions.Singleline | RegexOptions.Compiled);
            Match matchServer = regexServer.Match(this.ConnectionString);
            if( matchServer == null && matchServer.Groups.Count < 2 )
                throw new Exception("�޷��������ַ�������������������Ϣ��");

            String server = matchServer.Groups[1].Value;
            if (NetPortON.ConnValidate(server, 1433, 1000) == false)
            {
                throw new Exception(String.Format("�޷����ӵ����ݿ������{0}��1433�˿ڡ�", server));
            }
        }
        #endregion
    }

    /// <summary>SqlServerԪ����</summary>
    class SqlServerMetaData : RemoteDbMetaData
    {
        #region ����
        /// <summary>�Ƿ�SQL2005</summary>
        public Boolean IsSQL2005 { get { return (Database as SqlServer).IsSQL2005; } }

        /// <summary>0������</summary>
        public String level0type { get { return IsSQL2005 ? "SCHEMA" : "USER"; } }
        #endregion

        #region ����
        /// <summary>ȡ�����б�����</summary>
        /// <returns></returns>
        protected override List<IDataTable> OnGetTables(ICollection<String> names)
        {
            #region ���˵�����ֶ���Ϣ��������Ϣ
            IDbSession session = Database.CreateSession();

            //һ���԰����еı�˵�������
            DataTable DescriptionTable = null;

            var b = DbSession._ShowSQL;
            DbSession.ShowSQL = false;
            try
            {
                DescriptionTable = session.Query(DescriptionSql).Tables[0];
            }
            catch { }
            DbSession._ShowSQL = b;

            DataTable dt = GetSchema(_.Tables, null);
            if (dt == null || dt.Rows == null || dt.Rows.Count < 1) return null;

            b = DbSession._ShowSQL;
            DbSession.ShowSQL = false;
            try
            {
                AllFields = session.Query(SchemaSql).Tables[0];
                AllIndexes = session.Query(IndexSql).Tables[0];
            }
            catch { }
            DbSession._ShowSQL = b;
            #endregion

            // �г��û���
            DataRow[] rows = dt.Select(String.Format("({0}='BASE TABLE' Or {0}='VIEW') AND TABLE_NAME<>'Sysdiagrams'", "TABLE_TYPE"));
            rows = OnGetTables(names, rows);
            if (rows == null || rows.Length < 1) return null;

            List<IDataTable> list = GetTables(rows);
            if (list == null || list.Count < 1) return list;

            // ������ע
            foreach (IDataTable item in list)
            {
                DataRow[] drs = DescriptionTable == null ? null : DescriptionTable.Select("n='" + item.Name + "'");
                item.Description = drs == null || drs.Length < 1 ? "" : drs[0][1].ToString();
            }

            return list;
        }

        private DataTable AllFields = null;
        private DataTable AllIndexes = null;

        protected override void FixField(IDataColumn field, DataRow dr)
        {
            base.FixField(field, dr);

            var rows = AllFields == null ? null : AllFields.Select("����='" + field.Table.Name + "' And �ֶ���='" + field.Name + "'", null);
            if (rows != null && rows.Length > 0)
            {
                var dr2 = rows[0];

                field.Identity = GetDataRowValue<Boolean>(dr2, "��ʶ");
                field.PrimaryKey = GetDataRowValue<Boolean>(dr2, "����");
                field.NumOfByte = GetDataRowValue<Int32>(dr2, "ռ���ֽ���");
                field.Description = GetDataRowValue<String>(dr2, "�ֶ�˵��");
            }

            // ����Ĭ��ֵ
            if (!String.IsNullOrEmpty(field.Default))
            {
                field.Default = Trim(field.Default, "(", ")");
                field.Default = Trim(field.Default, "\"", "\"");
                field.Default = Trim(field.Default, "\'", "\'");
                field.Default = Trim(field.Default, "N\'", "\'");
                field.Default = field.Default.Replace("''", "'");

                // ��������CONVERT([datetime],'1753-1-1',(0))��ʱ��Ĭ��ֵ
                if (field.DataType == typeof(DateTime))
                {
                    var def = field.Default;
                    if (def.StartsWith("CONVERT(", StringComparison.OrdinalIgnoreCase))
                    {
                        var ss = def.Split(",");
                        if (ss.Length >= 3)
                        {
                            def = ss[1].Trim().Trim('\'').Trim();
                            DateTime dt;
                            if (DateTime.TryParse(def, out dt))
                            {
                                if (dt == Database.DateTimeMin || dt.Date == Database.DateTimeMin.Date)
                                {
                                    //def = Database.DateTimeNow;
                                    //def = Database.DateTimeMin.ToString("yyyy-MM-dd");
                                    field.Default = def;
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override List<IDataIndex> GetIndexes(IDataTable table)
        {
            List<IDataIndex> list = base.GetIndexes(table);
            if (list != null && list.Count > 0)
            {
                foreach (IDataIndex item in list)
                {
                    DataRow[] drs = AllIndexes == null ? null : AllIndexes.Select("name='" + item.Name + "'");
                    if (drs != null && drs.Length > 0)
                    {
                        item.Unique = GetDataRowValue<Boolean>(drs[0], "is_unique");
                        item.PrimaryKey = GetDataRowValue<Boolean>(drs[0], "is_primary_key");
                    }
                }
            }
            return list;
        }

        public override string CreateTableSQL(IDataTable table)
        {
            String sql = base.CreateTableSQL(table);
            if (String.IsNullOrEmpty(sql) || table.PrimaryKeys == null || table.PrimaryKeys.Length < 2) return sql;

            // ����������
            StringBuilder sb = new StringBuilder();
            foreach (IDataColumn item in table.PrimaryKeys)
            {
                if (sb.Length > 0) sb.Append(",");
                sb.Append(FormatName(item.Name));
            }
            sql += "; " + Environment.NewLine;
            sql += String.Format("Alter Table {0} Add Constraint PK_{1} Primary Key Clustered({2})", FormatName(table.Name), table.Name, sb.ToString());
            return sql;
        }

        public override string FieldClause(IDataColumn field, bool onlyDefine)
        {
            if (!String.IsNullOrEmpty(field.RawType) && field.RawType.Contains("char(-1)"))
            {
                if (IsSQL2005)
                    field.RawType = field.RawType.Replace("char(-1)", "char(MAX)");
                else
                    field.RawType = field.RawType.Replace("char(-1)", "char(" + (Int32.MaxValue / 2) + ")");
            }

            return base.FieldClause(field, onlyDefine);
        }

        protected override string GetFieldConstraints(IDataColumn field, Boolean onlyDefine)
        {
            // �Ƕ���ʱ���޸��ֶΣ��������ֶ�û��Լ��
            if (!onlyDefine && field.PrimaryKey) return null;

            String str = base.GetFieldConstraints(field, onlyDefine);

            // �Ƕ���ʱ�������ֶ�û��Լ��
            if (onlyDefine && field.Identity) str = " IDENTITY(1,1)" + str;

            return str;
        }

        protected override string GetFormatParam(IDataColumn field, DataRow dr)
        {
            String str = base.GetFormatParam(field, dr);
            if (String.IsNullOrEmpty(str)) return str;

            // �����Ҫ������float����Ϊ�޷�ȡ���侫��
            if (str == "(0)") return null;
            return str;
        }

        protected override string GetFormatParamItem(IDataColumn field, DataRow dr, string item)
        {
            String pi = base.GetFormatParamItem(field, dr, item);
            if (field.DataType == typeof(String) && pi == "-1" && IsSQL2005) return "MAX";
            return pi;
        }

        protected override string GetFieldDefault(IDataColumn field, bool onlyDefine)
        {
            if (!onlyDefine) return null;

            return base.GetFieldDefault(field, onlyDefine);
        }
        #endregion

        #region ȡ���ֶ���Ϣ��SQLģ��
        private String _SchemaSql = "";
        /// <summary>����SQL</summary>
        public virtual String SchemaSql
        {
            get
            {
                if (String.IsNullOrEmpty(_SchemaSql))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("SELECT ");
                    sb.Append("����=d.name,");
                    sb.Append("�ֶ����=a.colorder,");
                    sb.Append("�ֶ���=a.name,");
                    sb.Append("��ʶ=case when COLUMNPROPERTY( a.id,a.name,'IsIdentity')=1 then Convert(Bit,1) else Convert(Bit,0) end,");
                    sb.Append("����=case when exists(SELECT 1 FROM sysobjects where xtype='PK' and name in (");
                    sb.Append("SELECT name FROM sysindexes WHERE id = a.id AND indid in(");
                    sb.Append("SELECT indid FROM sysindexkeys WHERE id = a.id AND colid=a.colid");
                    sb.Append("))) then Convert(Bit,1) else Convert(Bit,0) end,");
                    sb.Append("����=b.name,");
                    sb.Append("ռ���ֽ���=a.length,");
                    sb.Append("����=COLUMNPROPERTY(a.id,a.name,'PRECISION'),");
                    sb.Append("С��λ��=isnull(COLUMNPROPERTY(a.id,a.name,'Scale'),0),");
                    sb.Append("������=case when a.isnullable=1 then Convert(Bit,1)else Convert(Bit,0) end,");
                    sb.Append("Ĭ��ֵ=isnull(e.text,''),");
                    sb.Append("�ֶ�˵��=isnull(g.[value],'')");
                    sb.Append("FROM syscolumns a ");
                    sb.Append("left join systypes b on a.xtype=b.xusertype ");
                    sb.Append("inner join sysobjects d on a.id=d.id  and d.xtype='U' ");
                    sb.Append("left join syscomments e on a.cdefault=e.id ");
                    if (IsSQL2005)
                    {
                        sb.Append("left join sys.extended_properties g on a.id=g.major_id and a.colid=g.minor_id and g.name = 'MS_Description'  ");
                    }
                    else
                    {
                        sb.Append("left join sysproperties g on a.id=g.id and a.colid=g.smallid  ");
                    }
                    sb.Append("order by a.id,a.colorder");
                    _SchemaSql = sb.ToString();
                }
                return _SchemaSql;
            }
        }

        private String _IndexSql;
        public virtual String IndexSql
        {
            get
            {
                if (_IndexSql == null)
                {
                    if (IsSQL2005)
                        _IndexSql = "select ind.* from sys.indexes ind inner join sys.objects obj on ind.object_id = obj.object_id where obj.type='u'";
                    else
                        _IndexSql = "select IndexProperty(obj.id, ind.name,'IsUnique') as is_unique, ObjectProperty(object_id(ind.name),'IsPrimaryKey') as is_primary_key,ind.* from sysindexes ind inner join sysobjects obj on ind.id = obj.id where obj.type='u'";
                }
                return _IndexSql;
            }
        }

        private readonly String _DescriptionSql2000 = "select b.name n, a.value v from sysproperties a inner join sysobjects b on a.id=b.id where a.smallid=0";
        private readonly String _DescriptionSql2005 = "select b.name n, a.value v from sys.extended_properties a inner join sysobjects b on a.major_id=b.id and a.minor_id=0 and a.name = 'MS_Description'";
        /// <summary>ȡ��˵��SQL</summary>
        public virtual String DescriptionSql { get { return IsSQL2005 ? _DescriptionSql2005 : _DescriptionSql2000; } }
        #endregion

        #region ���ݶ���
        public override object SetSchema(DDLSchema schema, params object[] values)
        {
            IDbSession session = Database.CreateSession();

            Object obj = null;
            String dbname = String.Empty;
            String databaseName = String.Empty;
            switch (schema)
            {
                case DDLSchema.DatabaseExist:
                    databaseName = values == null || values.Length < 1 ? null : (String)values[0];
                    if (String.IsNullOrEmpty(databaseName)) databaseName = session.DatabaseName;

                    dbname = session.DatabaseName;
                    session.DatabaseName = SystemDatabaseName;
                    try
                    {
                        obj = DatabaseExist(databaseName);
                    }
                    finally { session.DatabaseName = dbname; }
                    return obj;
                case DDLSchema.DropDatabase:
                    databaseName = values == null || values.Length < 1 ? null : (String)values[0];
                    if (String.IsNullOrEmpty(databaseName)) databaseName = session.DatabaseName;
                    values = new Object[] { databaseName, values == null || values.Length < 2 ? null : values[1] };

                    dbname = session.DatabaseName;
                    session.DatabaseName = SystemDatabaseName;
                    try
                    {
                        //obj = base.SetSchema(schema, values);
                        //if (Execute(String.Format("Drop Database [{0}]", dbname)) < 1)
                        //{
                        //    Execute(DropDatabaseSQL(databaseName));
                        //}
                        var sb = new StringBuilder();
                        sb.AppendLine("use master");
                        sb.AppendLine(";");
                        sb.AppendLine("declare   @spid   varchar(20),@dbname   varchar(20)");
                        sb.AppendLine("declare   #spid   cursor   for");
                        sb.AppendFormat("select   spid=cast(spid   as   varchar(20))   from   master..sysprocesses   where   dbid=db_id('{0}')", dbname);
                        sb.AppendLine();
                        sb.AppendLine("open   #spid");
                        sb.AppendLine("fetch   next   from   #spid   into   @spid");
                        sb.AppendLine("while   @@fetch_status=0");
                        sb.AppendLine("begin");
                        sb.AppendLine("exec('kill   '+@spid)");
                        sb.AppendLine("fetch   next   from   #spid   into   @spid");
                        sb.AppendLine("end");
                        sb.AppendLine("close   #spid");
                        sb.AppendLine("deallocate   #spid");

                        Int32 count = 0;
                        try { count = session.Execute(sb.ToString()); }
                        catch { }
                        obj = session.Execute(String.Format("Drop Database {0}", FormatName(dbname))) > 0;
                        //sb.AppendFormat("Drop Database [{0}]", dbname);
                    }
                    finally { session.DatabaseName = dbname; }
                    return obj;
                case DDLSchema.TableExist:
                    return TableExist((IDataTable)values[0]);
                default:
                    break;
            }
            return base.SetSchema(schema, values);
        }

        public override string CreateDatabaseSQL(string dbname, string file)
        {
            String dataPath = (Database as SqlServer).DataPath;

            if (String.IsNullOrEmpty(file))
            {
                if (String.IsNullOrEmpty(dataPath)) return String.Format("CREATE DATABASE {0}", FormatName(dbname));

                file = dbname + ".mdf";
            }

            String logfile = String.Empty;

            if (!Path.IsPathRooted(file))
            {
                if (!String.IsNullOrEmpty(dataPath)) file = Path.Combine(dataPath, file);

                if (!Path.IsPathRooted(file)) file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
            }
            if (String.IsNullOrEmpty(Path.GetExtension(file))) file += ".mdf";
            file = new FileInfo(file).FullName;

            logfile = Path.ChangeExtension(file, ".ldf");
            logfile = new FileInfo(logfile).FullName;

            String dir = Path.GetDirectoryName(file);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("CREATE DATABASE {0} ON  PRIMARY", FormatName(dbname));
            sb.AppendLine();
            sb.AppendFormat(@"( NAME = N'{0}', FILENAME = N'{1}', SIZE = 10 , MAXSIZE = UNLIMITED, FILEGROWTH = 10%)", dbname, file);
            sb.AppendLine();
            sb.Append("LOG ON ");
            sb.AppendLine();
            sb.AppendFormat(@"( NAME = N'{0}_Log', FILENAME = N'{1}', SIZE = 10 , MAXSIZE = UNLIMITED, FILEGROWTH = 10%)", dbname, logfile);
            sb.AppendLine();

            return sb.ToString();
        }

        public override string DatabaseExistSQL(string dbname)
        {
            return String.Format("SELECT * FROM sysdatabases WHERE name = N'{0}'", dbname);
        }

        /// <summary>ʹ�����ݼܹ�ȷ�����ݿ��Ƿ���ڣ���Ϊʹ��ϵͳ��ͼ����û��Ȩ��</summary>
        /// <param name="dbname"></param>
        /// <returns></returns>
        public Boolean DatabaseExist(string dbname)
        {
            DataTable dt = GetSchema(_.Databases, new String[] { dbname });
            return dt != null && dt.Rows != null && dt.Rows.Count > 0;
        }

        public override string TableExistSQL(String tableName)
        {
            if (IsSQL2005)
                return String.Format("select * from sysobjects where xtype='U' and name='{0}'", tableName);
            else
                return String.Format("SELECT * FROM sysobjects WHERE id = OBJECT_ID(N'[dbo].{0}') AND OBJECTPROPERTY(id, N'IsUserTable') = 1", FormatName(tableName));
        }

        /// <summary>ʹ�����ݼܹ�ȷ�����ݱ��Ƿ���ڣ���Ϊʹ��ϵͳ��ͼ����û��Ȩ��</summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public Boolean TableExist(IDataTable table)
        {
            DataTable dt = GetSchema(_.Tables, new String[] { null, null, table.Name, null });
            return dt != null && dt.Rows != null && dt.Rows.Count > 0;
        }

        public override string AddTableDescriptionSQL(IDataTable table)
        {
            return String.Format("EXEC dbo.sp_addextendedproperty @name=N'MS_Description', @value=N'{1}' , @level0type=N'{2}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}'", table.Name, table.Description, level0type);
        }

        public override string DropTableDescriptionSQL(IDataTable table)
        {
            return String.Format("EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'{1}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}'", table.Name, level0type);
        }

        public override string AddColumnSQL(IDataColumn field)
        {
            return String.Format("Alter Table {0} Add {1}", FormatName(field.Table.Name), FieldClause(field, true));
        }

        public override string AlterColumnSQL(IDataColumn field, IDataColumn oldfield)
        {
            // ����Ϊ��������ɾ���
            if (field.Identity && !oldfield.Identity)
            {
                return DropColumnSQL(oldfield) + ";" + Environment.NewLine + AddColumnSQL(field);
            }

            String sql = String.Format("Alter Table {0} Alter Column {1}", FormatName(field.Table.Name), FieldClause(field, false));
            String pk = DeletePrimaryKeySQL(field);
            if (field.PrimaryKey)
            {
                // ���û������ɾ���ű�������û������
                //if (String.IsNullOrEmpty(pk))
                if (!oldfield.PrimaryKey)
                {
                    // ��������Լ��
                    pk = String.Format("Alter Table {0} ADD CONSTRAINT PK_{0} PRIMARY KEY {2}({1}) ON [PRIMARY]", FormatName(field.Table.Name), FormatName(field.Name), field.Identity ? "CLUSTERED" : "");
                    sql += ";" + Environment.NewLine + pk;
                }
            }
            else
            {
                // �ֶ�����û����������������ʵ�ʴ��ڣ���ɾ������
                //if (!String.IsNullOrEmpty(pk))
                if (oldfield.PrimaryKey)
                {
                    sql += ";" + Environment.NewLine + pk;
                }
            }
            return sql;
        }

        public override string DropColumnSQL(IDataColumn field)
        {
            //ɾ��Ĭ��ֵ
            String sql = DropDefaultSQL(field);
            if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;

            //ɾ������
            String sql2 = DeletePrimaryKeySQL(field);
            if (!String.IsNullOrEmpty(sql2)) sql += sql2 + ";" + Environment.NewLine;

            sql += base.DropColumnSQL(field);
            return sql;
        }

        public override string AddColumnDescriptionSQL(IDataColumn field)
        {
            String sql = String.Format("EXEC dbo.sp_addextendedproperty @name=N'MS_Description', @value=N'{1}' , @level0type=N'{3}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}', @level2type=N'COLUMN',@level2name=N'{2}'", field.Table.Name, field.Description, field.Name, level0type);
            return sql;
        }

        public override string DropColumnDescriptionSQL(IDataColumn field)
        {
            return String.Format("EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'{2}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}', @level2type=N'COLUMN',@level2name=N'{1}'", field.Table.Name, field.Name, level0type);
        }

        public override string AddDefaultSQL(IDataColumn field)
        {
            String sql = DropDefaultSQL(field);
            if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;

            var tc = Type.GetTypeCode(field.DataType);

            String dv = field.Default;
            if (CheckAndGetDefault(field, ref dv))
            {
                if (String.IsNullOrEmpty(dv)) return sql;
                sql += String.Format("Alter Table {0} Add CONSTRAINT DF_{0}_{1} DEFAULT {2} FOR {1}", field.Table.Name, field.Name, dv);
                return sql;
            }

            if (tc == TypeCode.String)
                sql += String.Format("Alter Table {0} Add CONSTRAINT DF_{0}_{1} DEFAULT N'{2}' FOR {1}", field.Table.Name, field.Name, field.Default);
            //else if (tc == TypeCode.DateTime)
            //{
            //    String dv = CheckAndGetDefault(field, field.Default);
            //    sql += String.Format("Alter Table {0} Add CONSTRAINT DF_{0}_{1} DEFAULT {2} FOR {1}", field.Table.Name, field.Name, dv);
            //}
            else
                sql += String.Format("Alter Table {0} Add CONSTRAINT DF_{0}_{1} DEFAULT {2} FOR {1}", field.Table.Name, field.Name, field.Default);
            return sql;
        }

        public override string DropDefaultSQL(IDataColumn field)
        {
            if (String.IsNullOrEmpty(field.Default)) return String.Empty;

            String sql = null;
            if (IsSQL2005)
                sql = String.Format("select b.name from sys.tables a inner join sys.default_constraints b on a.object_id=b.parent_object_id inner join sys.columns c on a.object_id=c.object_id and b.parent_column_id=c.column_id where a.name='{0}' and c.name='{1}'", field.Table.Name, field.Name);
            else
                sql = String.Format("select b.name from syscolumns a inner join sysobjects b on a.cdefault=b.id inner join sysobjects c on a.id=c.id where a.name='{1}' and c.name='{0}' and b.xtype='D'", field.Table.Name, field.Name);

            DataSet ds = Database.CreateSession().Query(sql);
            if (ds == null || ds.Tables == null || ds.Tables[0].Rows.Count < 1) return null;

            StringBuilder sb = new StringBuilder();
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                String name = dr[0].ToString();
                if (sb.Length > 0) sb.AppendLine(";");
                sb.AppendFormat("Alter Table {0} Drop CONSTRAINT {1}", FormatName(field.Table.Name), name);
            }
            return sb.ToString();
        }

        String DeletePrimaryKeySQL(IDataColumn field)
        {
            if (!field.PrimaryKey) return String.Empty;

            if (field.Table.Indexes == null || field.Table.Indexes.Count < 1) return String.Empty;

            IDataIndex di = null;
            foreach (IDataIndex item in field.Table.Indexes)
            {
                if (Array.IndexOf(item.Columns, field.Name) >= 0)
                {
                    di = item;
                    break;
                }
            }
            if (di == null) return String.Empty;

            return String.Format("Alter Table {0} Drop CONSTRAINT {1}", FormatName(field.Table.Name), di.Name);
        }

        public override String DropDatabaseSQL(String dbname)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("use master");
            sb.AppendLine(";");
            sb.AppendLine("declare   @spid   varchar(20),@dbname   varchar(20)");
            sb.AppendLine("declare   #spid   cursor   for");
            sb.AppendFormat("select   spid=cast(spid   as   varchar(20))   from   master..sysprocesses   where   dbid=db_id('{0}')", dbname);
            sb.AppendLine();
            sb.AppendLine("open   #spid");
            sb.AppendLine("fetch   next   from   #spid   into   @spid");
            sb.AppendLine("while   @@fetch_status=0");
            sb.AppendLine("begin");
            sb.AppendLine("exec('kill   '+@spid)");
            sb.AppendLine("fetch   next   from   #spid   into   @spid");
            sb.AppendLine("end");
            sb.AppendLine("close   #spid");
            sb.AppendLine("deallocate   #spid");
            sb.AppendLine(";");
            sb.AppendFormat("Drop Database {0}", FormatName(dbname));
            return sb.ToString();
        }
        #endregion

        #region ��������
        /// <summary>��ȥ�ַ������˳ɶԳ��ֵķ���</summary>
        /// <param name="str"></param>
        /// <param name="prefix"></param>
        /// <param name="suffix"></param>
        /// <returns></returns>
        public static String Trim(String str, String prefix, String suffix)
        {
            while (!String.IsNullOrEmpty(str))
            {
                if (!str.StartsWith(prefix)) return str;
                if (!str.EndsWith(suffix)) return str;

                str = str.Substring(prefix.Length, str.Length - suffix.Length - prefix.Length);
            }
            return str;
        }
        #endregion
    }
}