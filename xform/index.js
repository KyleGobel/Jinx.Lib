var config = require('./config'),
	redis = require('redis'),
	_ = require('lodash'),
	client = redis.createClient(config.redis.port, config.redis.address, {});


if (config.redis.authRequired) {
	client.auth(config.redis.password, function() {});
}

