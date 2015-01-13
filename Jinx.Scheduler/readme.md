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

#####``Jinx:DataStores:Ids``

Holds all the data stores ids in a set, this is know as the ``BaseRedisKey`` they can be pointed anywhere in redis 
but standard convention should be something like ``Jinx:DataStores:DataStoreName``

#####``Jinx:DataStores:<DataStoreName> ``

The base key for the store, nearly all information about the store or jobs will be stored in a subkey here

#####``Jinx:DataStores:<DataStoreName>:Jobs``

    This is a hash set, the key of the hash entity can be anything, though if the particular type
    of job requires additional config, this key value will point to the redis key where the additional data is stored.  
    The value of the hash entry is a json representation of the ``JinxJobInfo`` class (contains general information 
    about the job).

    This hash value will contain things such as: JobKey, and TriggerKey, CronExpression, and the type of the job.





