﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace DigitalPlatform.LibraryServer
{
    /// <summary>
    /// 存储流通借还操作动作信息的数据库
    /// </summary>
    public class ChargingOperDatabase : MongoDatabase<ChargingOperItem>
    {
        public ChargingOperDatabase()
        {
            _databaseName = "chargingOper";
        }

        public override void CreateIndex()
        {
            _collection.CreateIndex(new IndexKeysBuilder().Ascending("OperTime"),
                IndexOptions.SetUnique(false));

            _collection.CreateIndex(new IndexKeysBuilder().Ascending("ItemBarcode"),
                IndexOptions.SetUnique(false));

            _collection.CreateIndex(new IndexKeysBuilder().Ascending("PatronBarcode"),
                IndexOptions.SetUnique(false));
        }

        // parameters:
        public bool Add(ChargingOperItem item)
        {
            MongoCollection<ChargingOperItem> collection = this._collection;
            if (collection == null)
                return false;

            collection.Insert(item);
            return true;
        }

        // parameters:
        //      patronBarcode   读者证条码号。如果 以 "@itemBarcode:" 前缀引导，表示这是册条码号
        public IMongoQuery BuildQuery(
            string patronBarcode,
            DateTime startTime,
            DateTime endTime,
            string operTypes)
        {
            var time_query = Query.And(Query.GTE("OperTime", startTime),
                Query.LT("OperTime", endTime));

            if (startTime == new DateTime(0) && endTime == new DateTime(0))
                time_query = Query.GTE("OperTime", startTime);
            else if (startTime == new DateTime(0))
                time_query = Query.LT("OperTime", endTime);
            else if (endTime == new DateTime(0))
                time_query = Query.GTE("OperTime", startTime);

            string itemBarcodePrefix = "@itemBarcode:";
            string itemRefIdPrefix = "@itemRefID:";

            IMongoQuery patron_query = null;
            if (patronBarcode != null
                && patronBarcode.StartsWith(itemBarcodePrefix) == true)
                patron_query = Query.EQ("ItemBarcode", patronBarcode.Substring(itemBarcodePrefix.Length));
            else if (patronBarcode != null
                && patronBarcode.StartsWith(itemRefIdPrefix) == true)
                patron_query = Query.EQ("ItemBarcode", "@refID:" + patronBarcode.Substring(itemRefIdPrefix.Length));
            else
                patron_query = Query.EQ("PatronBarcode", patronBarcode);

            List<IMongoQuery> action_items = new List<IMongoQuery>();
            string[] types = operTypes.Split(new char[] { ',' });
            foreach (string type in types)
            {
                if (type == "borrow")
                    action_items.Add(Query.EQ("Action", "borrow"));
                if (type == "return")
                    action_items.Add(Query.EQ("Action", "return"));
                if (type == "renew")
                    action_items.Add(Query.EQ("Action", "renew"));
                if (type == "lost")
                    action_items.Add(Query.EQ("Action", "lost"));
                if (type == "read")
                    action_items.Add(Query.EQ("Action", "read"));
            }

            var type_query = Query.And(Query.Or(Query.EQ("Operation", "borrow"), Query.EQ("Operation", "return")),
                Query.Or(action_items));

            return Query.And(patron_query, time_query, type_query);
        }

#if NO
        static IMongoQuery _rel_type_query = null;   // 存储起来，避免每次创建的消耗
                    if (_rel_type_query == null)
            {
                List<IMongoQuery> action_items = new List<IMongoQuery>();
                {
                    action_items.Add(Query.EQ("Action", "return"));
                    action_items.Add(Query.EQ("Action", "renew"));
                    action_items.Add(Query.EQ("Action", "lost"));
                }

                _rel_type_query = Query.And(Query.Or(Query.EQ("Operation", "borrow"), Query.EQ("Operation", "return")),
        Query.Or(action_items));
            }
#endif

        // 查找和本还书 item 关联的的借书操作 item
        public ChargingOperItem FindRelativeBorrowItem(ChargingOperItem return_item)
        {
            MongoCollection<ChargingOperItem> collection = this._collection;
            if (collection == null)
                return null;

            var query = Query.And(
                Query.And(Query.EQ("Operation", "borrow"), Query.EQ("Action", "borrow")),
                Query.EQ("PatronBarcode", return_item.PatronBarcode),
                Query.EQ("ItemBarcode", return_item.ItemBarcode),
                Query.LTE("OperTime", return_item.OperTime));
            // 获得最近的一个 borrow item
            MongoCursor<ChargingOperItem> cursor = collection.Find(query).SetSortOrder(SortBy.Descending("OperTime")).SetLimit(1);
            foreach (ChargingOperItem item in cursor.Take(1))
            {
                return item;
            }
            return null;
        }

        // parameters:
        //      order   排序方式。ascending/descending 之一。默认 ascending
        public IEnumerable<ChargingOperItem> Find(
            string patronBarcode,
            DateTime startTime,
            DateTime endTime,
            string operTypes,
            string order,
            int start,
            out long totalCount)
        {
            totalCount = 0;
            MongoCollection<ChargingOperItem> collection = this._collection;
            if (collection == null)
                return null;

            var query = BuildQuery(patronBarcode,
                startTime,
                endTime,
                operTypes);
            IMongoSortBy sortBy = SortBy.Ascending("OperTime");
            if (order == "descending")
                sortBy = SortBy.Descending("OperTime");

            MongoCursor<ChargingOperItem> cursor = collection.Find(query).SetSortOrder(sortBy);
            IEnumerable<ChargingOperItem> results = cursor.Skip(start);
            totalCount = cursor.Count();
            return results;
        }

        public int GetItemCount(IMongoQuery query)
        {
            MongoCollection<ChargingOperItem> collection = this._collection;
            if (collection == null)
                return -1;

            var keyFunction = (BsonJavaScript)@"{}";

            var document = new BsonDocument("count", 0);
            var result = collection.Group(
                query,
                keyFunction,
                document,
                new BsonJavaScript("function(doc, out){ out.count++; }"),
                null
            ).ToArray();

            foreach (BsonDocument doc in result)
            {
                return doc.GetValue("count", 0).ToInt32();
            }

            return 0;
        }

    }

    public class ChargingOperItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; private set; }

        public string LibraryCode { get; set; } // 访问者的图书馆代码
        public string Operation { get; set; } // 操作名
        public string Action { get; set; }  // 动作

        public string ItemBarcode { get; set; }
        public string PatronBarcode { get; set; }

        public string Period { get; set; }  // 期限
        public string No { get; set; }  // 续借次，序号

        public string ClientAddress { get; set; }  // 访问者的IP地址

        public string Operator { get; set; }  // 操作者(访问者)
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime OperTime { get; set; } // 操作时间
    }

}