--Table Creation
--******************--

drop table if exists jobs;
create table jobs (
	job_id serial primary key not null,
	job_details_id int4,
	name text not null,
	description text not null,
	group_name text not null,
	enabled bit not null,
	job_type text not null,
	job_key_name text,
	job_key_group text,
	trigger_key_name text,
	trigger_key_group text,
	cron_expression text
);

drop table if exists job_details;
create table job_details (
	job_details_id serial primary key not null,
	details text not null
);

drop table if exists events;
create table events (
	event_id serial primary key not null,
	timestamp timestamp not null,
	event_name text not null,
	data text
);

drop table if exists job_history;
create table job_history (
	job_history_id serial primary key not null,
	job_id int not null,
	timestamp timestamp not null,
	job_type text,
	job_data text,
	run_time int8,
	exception_data text
);


--Seed Database
--**********************

insert into jobs 
	(name, job_details_id, description, group_name, enabled, job_type, job_key_name, job_key_group, trigger_key_name, trigger_key_group, cron_expression)
values 
(
	'Test QueryRedis Job',
	1,
	'Run a query from sql and insert it into redis',
	'Test',
	'1',
	'SqlServerQuery',
	'TestQuery',
	'TestGroup',
	'TestQueryTrigger',
	'TestGroup',
	'0/30 * * 1/1 * ? *'
);

insert into job_details
	(job_details_id, details)
values 
(
	1, '{ "connectionKey" : "PkcMobDb01-Reporting", "query" : "select * from spend", "redisStorageKey": "SpendData"  }'
);
