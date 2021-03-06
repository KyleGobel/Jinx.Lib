#Jinx Scheduler

##Scheduler Job

The scheduler job is run every minute, and it's sole purpose is to update the job schedule, and reschedule/update jobs.

It first gets all data store description objects by checking the set located at redis key ``Jinx::DataStores:Ids``, which contains the redis id of every data store object.

Using these lookup keys it then looks up the DataStore objects which contain the base redis key in which to look for schedule/job information.




It uses the redis key ``Jinx:Schedule``.

Every minute it updates the in memory jobs list from the list in redis.

Serialized in redis an example entry in the list might look like this
```
{
"dataStoreKey": "urn:Users",
"name" : "Users",
"enabled" : "true"
}
```

This specifies that the data store Users is located at ``urn:Users`` and it is enabled (meaning jobs should be run).


###Keys breakdown

``Jinx:DataStores:Ids``

Holds all the data stores ids in a set, this is know as the ``BaseRedisKey`` they can be pointed anywhere in redis
but standard convention should be something like ``Jinx:DataStores:DataStoreName``

``Jinx:DataStores:<datastorename>``

The base key for the store, nearly all information about the store or jobs will be stored in a subkey here

``Jinx:DataStores:<datastorename>:Jobs``

This is a hash set, the key of the hash entity can be anything, though if the particular type
of job requires additional config, this key value will point to the redis key where the additional data is stored.
The value of the hash entry is a json representation of the ``JinxJobInfo`` class (contains general information
about the job).

This hash value will contain things such as: JobKey, and TriggerKey, CronExpression, and the type of the job.
Depending on the type of the job their may be additional configuration keys

``Jinx:DataStores<datastorename>:SqlServerQueryConfig``

If on of the entries in the hash set is of type ``SqlServerQuery`` then this key will contain the data required for the job
Currently this is just json with two keys, ``databaseConnectionKey``, and ``query``, the connection key is a key to the connection string, while the query
is the actual sql query that will be run.

``Jinx:DataStores<datastorename>:Data``

Where the main raw data is stored, typically a list, inserted with a right push, taken out with a left pop.  Depending on the job type, data will either be added
or removed to this key.

``Jinx:DataStores:<datastorename>:Transform"``

If one of the values of the hash set in Jobs is of type Transform, this will be the default key of that entry and this will hold information about the transform operation.

Serialized as json an example transform job could look like this

```
{
	"dataKey" : "Jinx:DataStores:Users:Data",
    "transformJs" : "function main(srcItems) { _.map(srcItems, function(i) { return i; }); }"
    "destinationKey" : "Jinx:DataStores:Users:DataOut"
}
```
The json will contain the key to find the source data, and then the transformJs will hold the javascript function to pipe the data through.

The destinationKey will be a key to wherever the transformed items will be saved to.  
