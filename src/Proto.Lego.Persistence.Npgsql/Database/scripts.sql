CREATE TABLE key_value_states
(
	key VARCHAR(500) PRIMARY KEY,
	value BYTEA
);

CREATE TABLE alive_workflows
(
	key VARCHAR(500) PRIMARY KEY
);