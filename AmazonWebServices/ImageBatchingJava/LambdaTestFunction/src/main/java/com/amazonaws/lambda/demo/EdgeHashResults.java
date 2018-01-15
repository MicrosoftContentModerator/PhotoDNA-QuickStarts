package com.amazonaws.lambda.demo;

public class EdgeHashResults {
	public String TrackingId;
	public MatchResults[] MatchResults;

	public class MatchResults {
		public Status Status;
		public Boolean IsMatch;
		public String ContentId;
		
	}
	
	public class Status {
		public int Code;
		public String Desription;
		public Object Exception;
	}
}
