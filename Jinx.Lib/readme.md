#Data Stores

A data store a grouping of items in redis that make up all info required to operate on a certain set of data

Each store should have a single base key

ie. ``urn:Users``

All additional information required to operate on this store will be located under this base key.

##Required Keys for all Stores

``JobTypes``

This key is a hash of all the jobs that should be scheduled for this data store.  An example key would look like

``urn:Users:JobTypes`` and the hash would contain strings of some of the defined job types.  

For example the Bulk Insert to SQL Server job has an ID of  ``BulkInsertToSqlServer``, so if this store is expected to be bulk inserted into sql server,
this job type would be located in the hash.

###Store    

Going with our above example of the *Users Store* (with a base key of ``urn:Users``) the data
for this store would be located at ``urn:Users:Data``.

All data is stored as a list data type, using one list entry for every entity stored.

The data will be either serialized as JSON, or serialized as byte arrays using google protocol buffers.


##Supported Stores

###Bulk Insert to SQL Server


    