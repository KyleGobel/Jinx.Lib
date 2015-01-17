#Data Stores

A data store a grouping of items in redis that make up all info required to operate on a certain set of data

Each store should have a single base key

ie. ``DataStores:Users``

All additional information required to operate on this store will be located under this base key.  A List of all the base keys should always be located at ``DataStores:Ids``.

##Required Keys for all Stores

``Jobs``

This key is a hash of all the jobs that should be scheduled for this data store.  An example key would look like ``DataStores:Users:Jobs``.  This hash is always located at ``<BaseStoreKey>:Jobs``.

The Hash Key is always a key pointer to a specific job object, the Hash Value is always a general job info object, which has a schema like this

```js
{
	//the type of job this is from the predefined list of jobtypes 
	//(Transform, SqlQuery, ect) 
	jobType:string,

	//this is a quartz job key, unique with jobGroup
	jobKeyName:string,
	//same as above, this is the job category, the combo of jobGroup 
	//and jobKey must be unique
	jobKeyGroup:string,

	//same as above, but the quartz trigger
	triggerKeyGroup:string,
	triggerKeyName:string

	//this specifies how often/when the job should be run
	cronExpression:string
}
```



###Store    

Going with our above example of the *Users Store* (with a base key of ``urn:Users``) the data
for this store would be located at ``urn:Users:Data``.

All data is stored as a list data type, using one list entry for every entity stored.

The data will be either serialized as JSON, or serialized as byte arrays using google protocol buffers.


##Supported Stores

###Bulk Insert to SQL Server

###Js Transform 

This is the map/reduce job.  Given a javascript function, a selected store will be piped through the transformer and the output data will be stored in this store.

####Required Keys for Transform

1.  An entry in the ``DataStores:Ids`` list to point to the main datastore
2.  An entry in the datastores Jobs hash.
	An entry here will have a hash entry with the key being another key to the TransformJob information, and the value being the general information about the job.
3.  The TransformJob object (serialized as JSON), this location is the same as the hash key in #2.

#####Example Layout

``DataStores:Ids``

Contains an entry that points to the datastore, and example might be ``DataStores:Users``

Inside the ``DataStores:Users:Jobs`` hash will be a listing of all the jobs and their types.

One of the entries should have the jobType of ``Transform``


key
```
DataStores:Users:TransformJob
```

value
```
{
	"jobType":"Transform",
	"jobKeyName":"TransformUsers",
	"jobKeyGroup":"MyProduct",
	"triggerKeyName":"TransformUsersTrigger",
	"triggerKeyGroup":"MyProduct",
	"cronExpression":"0/30 * * 1/1 * ? *"
}
```

Since the key of the Transform Job (in the jobs hash) is ``DataStores:Users:TransformJob`` that is where we can find the transform job info, which might look like this

```
{
    "dataKey": "Jinx:DataStores:GoogleCampaigns:Data",
    "destinationKey": "DataStores:GoogleCampTransform:DataOut",
    "transformJs": "function main(srcItems) { 
    					return _.map(srcItems, function(i) 
    					{ 
    						return  { date : i.dateCreated }; 
    					}); 
    				}"
}

```

With the dataKey being pointed to the list that you want to transform, the destination key being pointed to where you want to store the destination information, and the transformJs being the actual javascript function that will transform the data.

####Required Keys for Sql Query

Same as the other jobs, this requires the main data store entry to be in the ``DataStores:Ids`` list, as well as an entry in the ``<BaseDataStoreKey>:Jobs`` hash with a type of ``SqlServerQuery``, and the key of the hash pointing to the SqlServerQuery Object which contains other info about the query.

That object has a schema like this:

```js
{
	//the connection key (in the app.config) to 
	//run the query against
	databaseConnectionKey : string,

	//the sql query to run
	query: string
}
```

The data that is returned will be stored at ``<BaseDataStore>:Data`` as a list, pushing from the right.