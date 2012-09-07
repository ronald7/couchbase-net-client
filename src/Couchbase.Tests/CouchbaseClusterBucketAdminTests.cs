﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Couchbase.Configuration;
using NUnit.Framework.Constraints;
using System.Net;
using Couchbase.Management;
using Enyim.Caching.Memcached;
using System.Threading;

namespace Couchbase.Tests
{
	[TestFixture]
	public class CouchbaseClusterBucketAdminTests : CouchbaseClusterTestsBase
	{
		[Test]
		public void When_Listing_Buckets_Default_Bucket_Is_Returned()
		{
			var buckets = _Cluster.ListBuckets();
			Assert.That(buckets.FirstOrDefault(b => b.Name == "default"), Is.Not.Null, "default bucket was not found");
		}

		[Test]
		[ExpectedException(typeof(ArgumentNullException))]
		public void When_Listing_Buckets_With_Invalid_Config_Argument_Exception_Is_Thrown()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/doesnotexist/"));
			config.Bucket = "default";
			var server = new CouchbaseCluster(config);
			var buckets = server.ListBuckets();
		}

		[Test]
		public void When_Try_Listing_Buckets_Default_Bucket_Is_Returned()
		{
			Bucket[] buckets;
			var result = _Cluster.TryListBuckets(out buckets);
			Assert.That(buckets.FirstOrDefault(b => b.Name == "default"), Is.Not.Null, "default bucket was not found");
			Assert.That(result, Is.True);
		}

		[Test]
		public void When_Try_Listing_Buckets_With_Invalid_Config_No_Exception_Is_Thrown_And_Return_Value_Is_False()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/doesnotexist/"));
			config.Bucket = "default";

			var server = new CouchbaseCluster(config);
			Bucket[] buckets;
			var result = server.TryListBuckets(out buckets);
			Assert.That(result, Is.False);
		}

		[Ignore("Restful flush has been delayed")]
		[Test]
		public void When_Flushing_Bucket_Data_Are_Removed()
		{
			var config = new CouchbaseClientConfiguration();
			config.Urls.Add(new Uri("http://localhost:8091/pools/default"));
			config.Bucket = "default";

			var client = new CouchbaseClient(config);
			var storeResult = client.ExecuteStore(StoreMode.Set, "SomeKey", "SomeValue");

			Assert.That(storeResult.Success, Is.True);

			var getResult = client.ExecuteGet<string>("SomeKey");
			Assert.That(getResult.Success, Is.True);
			Assert.That(getResult.Value, Is.StringMatching("SomeValue"));

			_Cluster.FlushBucket("default");

			getResult = client.ExecuteGet<string>("SomeKey");
			Assert.That(getResult.Success, Is.False);
			Assert.That(getResult.Value, Is.Null);
		}

		[Test]
		public void When_Creating_New_Bucket_That_Bucket_Is_Listed()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			}
			);

			var bucket = waitForListed(bucketName);

			Assert.That(bucket, Is.Not.Null);

			_Cluster.DeleteBucket(bucketName);

		}

		[Test]
		public void When_Creating_New_Memcached_Bucket_That_Bucket_Is_Listed()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.None,
				BucketType = BucketTypes.Memcached,
				RamQuotaMB = 100,
				ProxyPort = 9090
			}
			);

			var bucket = waitForListed(bucketName);

			Assert.That(bucket, Is.Not.Null);

			_Cluster.DeleteBucket(bucketName);

		}

		[Test]
		[ExpectedException(typeof(WebException))]
		public void When_Creating_New_Bucket_With_Existing_Name_Web_Exception_Is_Thrown()
		{
			_Cluster.CreateBucket(new Bucket
			{
				Name = "default",
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 128
			});
		}

		[Test]
		[ExpectedException(typeof(ArgumentException), ExpectedMessage = "ProxyPort", MatchType = MessageMatch.Contains)]
		public void When_Creating_New_Bucket_With_Auth_Type_None_And_No_Port_Argument_Exception_Is_Thrown()
		{
			_Cluster.CreateBucket(new Bucket
			{
				Name = "default",
				AuthType = AuthTypes.None,
				BucketType = BucketTypes.Memcached,
				RamQuotaMB = 128
			});
		}

		[Test]
		[ExpectedException(typeof(ArgumentException), ExpectedMessage = "ProxyPort", MatchType = MessageMatch.Contains)]
		public void When_Creating_New_Bucket_With_Auth_Type_Sasl_And_Port_Argument_Exception_Is_Thrown()
		{
			_Cluster.CreateBucket(new Bucket
			{
				Name = "default",
				AuthType = AuthTypes.None,
				BucketType = BucketTypes.Memcached,
				RamQuotaMB = 128
			});
		}


		[Test]
		[ExpectedException(typeof(ArgumentException), ExpectedMessage = "RamQuotaMB", MatchType = MessageMatch.Contains)]
		public void When_Creating_New_Bucket_With_Ram_Quota_Less_Than_100_Argument_Exception_Is_Thrown()
		{
			_Cluster.CreateBucket(new Bucket
			{
				Name = "default",
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Memcached,
				RamQuotaMB = 99
			});
		}

		[Test]
		public void When_Deleting_Bucket_Bucket_Is_No_Longer_Listed()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.CreateBucket(new Bucket
			{
				Name = bucketName,
				AuthType = AuthTypes.Sasl,
				BucketType = BucketTypes.Membase,
				RamQuotaMB = 100
			});

			var bucket = waitForListed(bucketName);

			Assert.That(bucket, Is.Not.Null, "New bucket was null");

			_Cluster.DeleteBucket(bucketName);

			bucket = waitForListed(bucketName);

			Assert.That(bucket, Is.Null, "Deleted bucket still exists");
		}

		[Test]
		[ExpectedException(typeof(WebException), ExpectedMessage = "404", MatchType = MessageMatch.Contains)]
		public void When_Deleting_Bucket_That_Does_Not_Exist_404_Web_Exception_Is_Thrown()
		{
			var bucketName = "Bucket-" + DateTime.Now.Ticks;

			_Cluster.DeleteBucket(bucketName);
		}

		private Bucket waitForListed(string bucketName, int ubound = 10, int miliseconds = 1000)
		{
			//Wait 10 seconds for bucket creation
			Bucket bucket = null;
			for (var i = 0; i < ubound; i++)
			{
				bucket = _Cluster.ListBuckets().Where(b => b.Name == bucketName).FirstOrDefault();
				if (bucket != null) break;
				Thread.Sleep(1000);
			}

			return bucket;
		}
	}
}

#region [ License information          ]
/* ************************************************************
 * 
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
 *    
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *    
 *        http://www.apache.org/licenses/LICENSE-2.0
 *    
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *    
 * ************************************************************/
#endregion