<?php
/**
 * WebEngine CMS
 * https://webenginecms.org/
 * 
 * @version 1.2.6
 * @author Lautaro Angelico <http://lautaroangelico.com/>
 * @copyright (c) 2013-2025 Lautaro Angelico, All Rights Reserved
 * 
 * Licensed under the MIT license
 * http://opensource.org/licenses/MIT
 */

// PHP Timezone
// http://php.net/manual/en/timezones.php
date_default_timezone_set('America/Sao_Paulo');

// Mu Chila: agenda lida dos arquivos de evento do servidor (C:\MuServer\Data\Event)
$eventTimes = require(__DIR__ . '/../includes/muchila/eventos.php');

// DO NOT EDIT BELOW THIS LINE

function getEventNextTime($eventSchedule) {
	$currentTime = date("H:i");
	foreach($eventSchedule as $time) {
		if($time > $currentTime) {
			return date("Y-m-d ") . $time;
		}
	}
	$tomorrow = date('d', strtotime('tomorrow'));
	return date("Y-m-$tomorrow ") . $eventSchedule[0];
}

function getEventPreviousTime($eventSchedule) {
	$currentTime = date("H:i");
	foreach($eventSchedule as $key => $time) {		
		if($time > $currentTime) {
			$last = $key-1;
			if($last < 0) {
				$yesterday = date('d', strtotime('yesterday'));
				return date("Y-m-$yesterday ") . end($eventSchedule);
			}
			return date("Y-m-d ") . $eventSchedule[$last];
			return;
		}
	}
	return date("Y-m-d ") . end($eventSchedule);
}

function getWeeklyEventNextTime($day, $time) {
	$currentDay = strtolower(date("l"));
	$currentTime = date("H:i");
	if($currentDay == strtolower($day)) {
		if($currentTime < $time) {
			return date("Y-m-d H:i", strtotime('today '.$time.''));
		}
	}
	return date("Y-m-d H:i", strtotime('next '.$day.' '.$time.''));
}

function getWeeklyEventPreviousTime($day, $time) {
	$currentDay = strtolower(date("l"));
	$currentTime = date("H:i");
	if($currentDay == strtolower($day)) {
		if($currentTime > $time) {
			return date("Y-m-d H:i", strtotime('today '.$time.''));
		}
	}
	return date("Y-m-d H:i", strtotime('last '.$day.' '.$time.''));
}

foreach($eventTimes as $eventId => $event) {
	$active = 0;
	$open = 0;
	if(!array_key_exists('day', $event)) {
		$lastTime = getEventPreviousTime($event['schedule']);
		$nextTime = getEventNextTime($event['schedule']);
	} else {
		$lastTime = getWeeklyEventPreviousTime($event['day'], $event['time']);
		$nextTime = getWeeklyEventNextTime($event['day'], $event['time']);
	}
	$nextTimeF = date("d/m H:i", strtotime($nextTime));
	$offset = strtotime($nextTime)-strtotime($lastTime);
	$timeLeft = strtotime($nextTime)-time();
	
	$result[$eventId] = array(
		'event' => $event['name'],
		'opentime' => $event['opentime'],
		'duration' => $event['duration'],
		'last' => $lastTime,
		'next' => $nextTime,
		'nextF' => $nextTimeF,
		'offset' => $offset,
		'timeleft' => $timeLeft,
	);
}

if(isset($_GET['event'])) {
	if(array_key_exists($_GET['event'], $result)) {
		$result = $result[$_GET['event']];
	}
}

http_response_code(200);
header('Content-Type: application/json');
echo json_encode($result, JSON_PRETTY_PRINT);