var config = require('./config'),
	redis = require('redis'),
	_ = require('lodash'),
	client = redis.createClient(config.redis.port, config.redis.address, {});


if (config.redis.authRequired) {
	client.auth(config.redis.password, function() {});
}

if (process.argv.length < 3) {
	console.log("Not enough command line arguments")
	process.exit(-1);
}

if (config.redis.database !== 0) {
	client.select(config.redis.database, function() {});
}

function xformMain() {
	var jobKey = process.argv[2];
	client.get(jobKey, function (err, value) {
		if (err) throw(error);
		console.log(value.toString());
	});
}

xformMain();

//list of data
//var elements = client.lrange(job.dataKey)
