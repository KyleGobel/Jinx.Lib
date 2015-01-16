var config = require('./config'),
	redis = require('redis'),
	_ = require('lodash'),
	client = redis.createClient(config.redis.port, config.redis.address, {});


if (config.redis.authRequired) {
	client.auth(config.redis.password, function() {});
}

if (process.argv.length < 3) {
	console.log("Not enough command line arguments, pass in the job key")
	process.exit(-1);
}

if (config.redis.database !== 0) {
	client.select(config.redis.database, function() {});
}

function xformMain() {
	var jobKey = process.argv[2];
	client.get(jobKey, function (err, value) {
		if (err) throw(error);
		var dataObj = JSON.parse(value); 

		console.log(dataObj);
		var dataKey = dataObj.dataKey;

		eval(dataObj.transformJs);
		var dataLength = 0;
		do {
			client.llen(dataKey, function(err,val) {
				var dataLength = val;
				if (dataLength > config.itemsToTakePerIteration) {
					dataLength = config.ItemsToTakePerIteration;
				}

				client.lrange(dataKey, 0, dataLength, function(err, vals) {
					//hope they defined a main method :)
					if (typeof main === 'function') {
						var items = main(vals);
						client.rpush(dataObj.destinationKey, items);
					}
					else {
						console.log("Couldn't find main function: " + typeof main);
					}
				});

			});
		} while (dataLength != 0)
		//get data







		
	});
}

xformMain();

//list of data
//var elements = client.lrange(job.dataKey)
