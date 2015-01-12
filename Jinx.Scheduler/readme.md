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

