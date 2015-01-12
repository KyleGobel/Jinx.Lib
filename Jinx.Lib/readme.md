#Data Stores

A data store a grouping of items in redis that make up all info required to operate on a certain set of data

Each store should have a single base key

ie. ``urn:Users``

All additional information required to operate on this store will be located under this base key.

##Required Keys for all Stores

###Store    

Going with our above example of the *Users Store* (with a base key of ``urn:Users``) the data
for this store would be located at ``urn:Users:Data``.

All data is stored as a list data type, using one list entry for every entity stored.

The data will be either serialized as JSON, or serialized as byte arrays using google protocol buffers.


##Supported Stores

###Bulk Insert to SQL Server


    