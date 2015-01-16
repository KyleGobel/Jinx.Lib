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
		eval(dataObj.transformJs);

		//hope they defined a main method :)
		if (typeof main === 'function') {
			main();
		}
		else {
			console.log("Couldn't find main function: " + typeof main);
		}
	});
}

xformMain();

//list of data
//var elements = client.lrange(job.dataKey)
