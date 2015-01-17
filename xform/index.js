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
		var dataObj = JSON.parse(value),
			dataKey = dataObj.dataKey,
			dataLength = 0;

		eval(dataObj.transformJs);
		do {
			client.llen(dataKey, function(err,val) {
				dataLength = val;
				if (dataLength > config.itemsToTakePerIteration) {
					dataLength = config.ItemsToTakePerIteration;
				}

				client.lrange(dataKey, 0, dataLength, function(err, vals) {
					//hope they defined a main method :)
					if (typeof main === 'function') {
						//turn items into javascript objects
						console.log("Converting store to JSON objects");
						var items = _.map(vals, function(i) {return JSON.parse(i);});
						console.log("Tranforming objects");
						items = main(items);
						//turn them back into json
						console.log("Converting objects to JSON");
						items = _.map(items, function(i) { return JSON.stringify(i);});
						items.unshift(dataObj.destinationKey);
						console.log("Inserting items");
						client.send_command('rpush',items, function() {
							console.log('Insertion complete...deleting items from store');
							client.ltrim(dataKey, dataLength, -1, function(err, val){
								console.log("All Operations Complete");
								process.exit(0);
							})
						});
						//client.rpush.apply(dataObj.destinationKey, items);
					}
					else {
						console.log("Couldn't find main function: " + typeof main);
					}
				});
			});
		} while (dataLength != 0)
	});
}

xformMain();

